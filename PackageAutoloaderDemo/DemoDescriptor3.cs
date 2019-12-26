using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PackageAutoloader.Models;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Archiving;
using Sitecore.Pipelines;

namespace PackageAutoloaderDemo
{
	public class DemoDescriptor3 : PackageFileLoaderDescriptor
	{
		public override List<Type> Dependencies => new List<Type>
		{
			typeof(DemoDescriptor2)
		};

		public override List<DescriptorItemRequirements> Requirements => new List<DescriptorItemRequirements>()
		{
			new DescriptorItemRequirements()
			{
				Database = "master",
				ItemId = new ID("{FEAB7DBD-7FFA-405F-939D-402093C02A81}")
			}
		};

		public override string RelativeFilePath => "/PackageAutoloader/demo3.zip";
	}
}
