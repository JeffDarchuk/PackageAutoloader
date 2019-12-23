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
	public class DemoDescriptor : PackageAutoloaderDescriptor
	{
		public void Process(PipelineArgs args)
		{
		}
		public override string PackageNamespace => "PackageAutoloaderDemo.demo.zip";
		public override List<DescriptorItemRequirements> Requirements => new List<DescriptorItemRequirements>()
		{
			new DescriptorItemRequirements()
			{
				Database = "master",
				ItemId = new ID("{76036F5E-CBCE-46D1-AF0A-4143F9B557AA}")
			}
		};


	}
}
