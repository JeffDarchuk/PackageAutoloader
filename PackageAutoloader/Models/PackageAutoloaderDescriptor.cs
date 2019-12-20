using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.Data;

namespace PackageAutoloader.Models
{
	public abstract class PackageAutoloaderDescriptor
	{
		public bool AllDescriptorItemRequirementsMustBeValid = true;

		public virtual bool CustomRequirement()
		{
			return true;
		}
		public abstract string PackageNamespace { get; }
		public abstract List<DescriptorItemRequirements> Requirements { get; }
		public class DescriptorItemRequirements
		{
			public string Database = "master";
			public ID ItemId;
			public List<KeyValuePair<ID, string>> RequiredFields;
		}
	}
}
