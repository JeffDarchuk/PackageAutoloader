using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web;
using PackageAutoloader.Models;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Engines;
using Sitecore.Diagnostics;
using Sitecore.Eventing;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Items;
using Sitecore.Install.Utils;
using Sitecore.Pipelines;
using Sitecore.SecurityModel;

namespace PackageAutoloader.Pipelines.Initialize
{
	public class InstallPackages
	{
		public void Process(PipelineArgs args)
		{
			foreach (var descriptor in GetDescriptors())
			{
				bool valid = true;
				if (!descriptor.CustomRequirement())
				{
					valid = false;
					Log.Info(
						$"PackageAutoLoader: Package {descriptor.PackageNamespace} will not be installed because it doesn't pass a custom requirement",
						this);
				}
				else
				{
					foreach (var requirement in descriptor.Requirements)
					{
						if (descriptor.AllDescriptorItemRequirementsMustBeValid)
						{
							valid = true;
						}

						if (!string.IsNullOrWhiteSpace(requirement.Database))
						{
							var db = Factory.GetDatabase(requirement.Database, false);
							if (db != null)
							{
								var item = db.GetItem(requirement.ItemId);
								if (item != null)
								{
									if (requirement.RequiredFields != null && requirement.RequiredFields.All(x => item[x.Key] == x.Value))
									{
										Log.Info(
											$"PackageAutoLoader: Package {descriptor.PackageNamespace} will not be installed because there is no detected field changes",
											this);
										valid = false;
										if (descriptor.AllDescriptorItemRequirementsMustBeValid)
											break;
									}

									if (requirement.RequiredFields == null)
									{
										Log.Info($"PackageAutoLoader: Package {descriptor.PackageNamespace} will not be installed because the key item already exists",
											this);
										valid = false;
										if (descriptor.AllDescriptorItemRequirementsMustBeValid)
											break;
									}
								}
							}
							else
							{
								Log.Info(
									$"PackageAutoLoader: Package {descriptor.PackageNamespace} will not be installed because the database {requirement.Database} is not available.",
									this);
								valid = false;
								if (descriptor.AllDescriptorItemRequirementsMustBeValid)
									break;
							}
						}
						else
						{
							Log.Info(
								$"PackageAutoLoader: Package {descriptor.PackageNamespace} will not be installed because the database is white space on the descriptor, database is a required property.",
								this);
							valid = false;
							if (descriptor.AllDescriptorItemRequirementsMustBeValid)
								break;
						}

						if (descriptor.AllDescriptorItemRequirementsMustBeValid && valid)
							break;
					}
				}

				if (!valid)
					break;
				Log.Info($"PackageAutoLoader: Installing Package {descriptor.PackageNamespace}", this);
				InstallSitecorePackage(descriptor);
				Log.Info($"PackageAutoLoader: Finished Installing Package {descriptor.PackageNamespace}", this);
			}
		}

		protected virtual IEnumerable<PackageAutoloaderDescriptor> GetDescriptors()
		{
			return AppDomain.CurrentDomain
				.GetAssemblies()
				.Where(x => !Constants.BinaryBlacklist.Contains(x.GetName().Name))
				.SelectMany(GetDescriptorTypes)
				.Select(t =>
				{
					try
					{
						return (PackageAutoloaderDescriptor)Activator.CreateInstance(t);
					}
					catch (MissingMethodException mex)
					{
						throw new InvalidOperationException($"Descriptor {t.FullName} could not be activated.", mex);
					}
				});
		}
		private IEnumerable<Type> GetDescriptorTypes(Assembly a)
		{
			IEnumerable<Type> types = null;
			try
			{
				types = a.GetTypes().Where(t => t.IsSubclassOf(typeof(PackageAutoloaderDescriptor)) && !t.IsAbstract);
			}
			catch (ReflectionTypeLoadException e)
			{
				types = e.Types.Where(t => t != null && t.IsSubclassOf(typeof(PackageAutoloaderDescriptor)) && !t.IsAbstract);
			}

			foreach (var type in types)
			{
				yield return type;
			}
		}

		protected virtual void InstallSitecorePackage(PackageAutoloaderDescriptor descriptor)
		{
			string filePath;
			var arr = descriptor.PackageNamespace.Split('.');
			string fileName = string.Join(".", arr.Skip(arr.Length-2).Take(2));
			if (System.Text.RegularExpressions.Regex.IsMatch(Settings.DataFolder, @"^(([a-zA-Z]:\\)|(//)).*"))
				//if we have an absolute path, rather than relative to the site root
				filePath = Settings.DataFolder +
				           @"\packages\PackageAutoLoader\";
			else
				filePath = HttpRuntime.AppDomainAppPath + Settings.DataFolder.Substring(1) +
						   @"\packages\PackageAutoLoader\";
			if (!Directory.Exists(filePath))
			{
				Directory.CreateDirectory(filePath);
			}
			filePath += fileName;
			try
			{
				using (var manifestResourceStream = descriptor.GetType().Assembly
					.GetManifestResourceStream(descriptor.PackageNamespace))
				using (var file = new FileStream(filePath, FileMode.Create))
				{
					manifestResourceStream?.CopyTo(file);
				}

				int count = 0;
				while (true)
				{
					if (!IsFileLocked(new FileInfo(filePath)))
					{
						using (new SecurityDisabler())
						using (new SyncOperationContext())
						{
							IProcessingContext context = new SimpleProcessingContext();
							IItemInstallerEvents events =
								new DefaultItemInstallerEvents(
									new BehaviourOptions(InstallMode.Overwrite, MergeMode.Undefined));
							context.AddAspect(events);
							IFileInstallerEvents events1 = new DefaultFileInstallerEvents(true);
							context.AddAspect(events1);

							Sitecore.Install.Installer installer = new Sitecore.Install.Installer();
							installer.InstallPackage(MainUtil.MapPath(filePath), context);
							break;
						}
					}

					Thread.Sleep(1000);
					count++;
					if (count > 15)
						Log.Error($"Unable to install package using Package Autoloader {descriptor.PackageNamespace} due to the package being locked.", this);
				}
			}
			catch (Exception e)
			{
				Log.Error($"Unable to install package using Package Autoloader {descriptor.PackageNamespace}", e, this);
			}
		}
		/// <summary>
		/// checks to see if the file is done being written to the filesystem
		/// </summary>
		/// <param name="file"></param>
		/// <returns></returns>
		protected virtual bool IsFileLocked(FileInfo file)
		{
			FileStream stream = null;

			try
			{
				stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
			}
			catch (IOException)
			{
				//the file is unavailable because it is:
				//still being written to
				//or being processed by another thread
				//or does not exist (has already been processed)
				return true;
			}
			finally
			{
				stream?.Close();
			}

			//file is not locked
			return false;
		}
	}
}
