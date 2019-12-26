using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Diagnostics;

namespace PackageAutoloader.Models
{
	public abstract class PackageAutoloaderDescriptor : DescriptorBase
	{
		public override string GetRelativeFilePath()
		{
			return GetAutoloaderFilePath();
		}

		public abstract string PackageNamespace { get; }

		public virtual string GetAutoloaderFilePath()
		{
			string filePath;
			var arr = PackageNamespace.Split('.');
			string fileName = string.Join(".", arr.Skip(arr.Length - 2).Take(2));
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
				using (var manifestResourceStream = GetType().Assembly
					.GetManifestResourceStream(PackageNamespace))
				using (var file = new FileStream(filePath, FileMode.Create))
				{
					manifestResourceStream?.CopyTo(file);
				}

				int count = 0;
				while (true)
				{
					if (!IsFileLocked(new FileInfo(filePath)))
					{
						return filePath;
					}

					Thread.Sleep(1000);
					count++;
					if (count > 15)
						Log.Error($"Unable to install package using Package Autoloader {PackageNamespace} due to the package being locked.", this);
				}
			}
			catch (Exception e)
			{
				Log.Error($"Unable to install package using Package Autoloader {PackageNamespace}", e, this);

			}

			throw new Exception("Can't get file path for Package Auto Loader.");
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
