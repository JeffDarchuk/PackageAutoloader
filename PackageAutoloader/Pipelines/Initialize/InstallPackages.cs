using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web;
using PackageAutoloader.Models;
using Sitecore;
using Sitecore.Common;
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
			HashSet<Type> alreadyInstalled = new HashSet<Type>();
			Stack<DescriptorBase> s = new Stack<DescriptorBase>();
			foreach (var descriptor in GetDescriptors())
			{
				bool valid = true;
				if (!descriptor.CustomRequirement())
				{
					valid = false;
					Log.Info(
						$"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because it doesn't pass a custom requirement",
						this);
					alreadyInstalled.Add(descriptor.GetType());
				}
				else
				{
					foreach (var requirement in descriptor.Requirements ?? Enumerable.Empty<DescriptorBase.DescriptorItemRequirements>())
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
										alreadyInstalled.Add(descriptor.GetType());
										Log.Info(
											$"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because there is no detected field changes",
											this);
										valid = false;
										if (descriptor.AllDescriptorItemRequirementsMustBeValid)
											break;
									}

									if (requirement.RequiredFields == null)
									{
										alreadyInstalled.Add(descriptor.GetType());
										Log.Info($"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because the key item already exists",
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
									$"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because the database {requirement.Database} is not available.",
									this);
								valid = false;
								if (descriptor.AllDescriptorItemRequirementsMustBeValid)
									break;
							}
						}
						else
						{
							Log.Info(
								$"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because the database is white space on the descriptor, database is a required property.",
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
					continue;

				s.Push(descriptor);
			}
			Dictionary<string, int> installCount = new Dictionary<string, int>();
			while (s.Any())
			{
				var descriptor = s.Pop();
				if (descriptor.Dependencies == null || descriptor.Dependencies.All(x => alreadyInstalled.Contains(x)))
				{
					Log.Info($"PackageAutoLoader: Installing Package {descriptor.GetType().FullName}", this);
					InstallSitecorePackage(descriptor);
					Log.Info($"PackageAutoLoader: Finished Installing Package {descriptor.GetType().FullName}", this);
				}
				else if (descriptor.Dependencies != null)
				{
					var id = descriptor.GetType().AssemblyQualifiedName;
					if (!installCount.ContainsKey(id))
					{
						installCount.Add(id, 0);
					}
					else if (installCount[id] > 10)
					{
						throw new Exception($"Unable to install {id} because dependencies can't be resolved.");
					}
					else
					{
						installCount[id] = installCount[id] + 1;
					}
					s.Push(descriptor);
				}
			}
		}

		protected virtual IEnumerable<DescriptorBase> GetDescriptors()
		{
			return AppDomain.CurrentDomain
				.GetAssemblies()
				.Where(x => !Constants.BinaryBlacklist.Contains(x.GetName().Name))
				.SelectMany(GetDescriptorTypes)
				.Select(t =>
				{
					try
					{
						return (DescriptorBase)Activator.CreateInstance(t);
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
				types = a.GetTypes().Where(t => t.IsSubclassOf(typeof(DescriptorBase)) && !t.IsAbstract);
			}
			catch (ReflectionTypeLoadException e)
			{
				types = e.Types.Where(t => t != null && t.IsSubclassOf(typeof(DescriptorBase)) && !t.IsAbstract);
			}

			foreach (var type in types)
			{
				yield return type;
			}
		}

		public virtual void InstallSitecorePackage(DescriptorBase descriptor)
		{
			using (new SecurityDisabler())
			using (new SyncOperationContext())
			{
				IProcessingContext context = new SimpleProcessingContext();
				context.AddAspect(descriptor.ItemInstallerEvents);
				context.AddAspect(descriptor.FileInstallerEvents);

				Sitecore.Install.Installer installer = new Sitecore.Install.Installer();
				installer.InstallPackage(MainUtil.MapPath(descriptor.GetRelativeFilePath()), context);
			}
		}
	}
}
