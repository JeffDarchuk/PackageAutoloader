using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PackageAutoloader.Models;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Archiving;
using Sitecore.Install.Files;
using Sitecore.Install.Items;
using Sitecore.Install.Utils;
using Sitecore.Pipelines;

namespace PackageAutoloaderDemo
{
	public class DemoDescriptor2 : PackageFileLoaderDescriptor
	{
		public override IItemInstallerEvents ItemInstallerEvents => 
			new DefaultItemInstallerEvents(new BehaviourOptions(InstallMode.Overwrite, MergeMode.Undefined));

		public override List<DescriptorItemRequirements> Requirements => new List<DescriptorItemRequirements>()
		{
			new DescriptorItemRequirements()
			{
				Database = "master",
				ItemId = new ID("{190B1C84-F1BE-47ED-AA41-F42193D9C8FC}")
			}
		};

		public override string RelativeFilePath => "/PackageAutoloader/demo2.zip";
	}
}
