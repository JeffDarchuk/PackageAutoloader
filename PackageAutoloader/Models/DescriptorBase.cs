using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.Data;
using Sitecore.Install.Files;
using Sitecore.Install.Items;
using Sitecore.Install.Utils;

namespace PackageAutoloader.Models
{
	public abstract class DescriptorBase
	{
		public abstract string GetRelativeFilePath();
		public virtual bool AllDescriptorItemRequirementsMustBeValid => true;
		public virtual bool AllItemsExist => false;
		public virtual List<Type> Dependencies => null;

		public virtual IItemInstallerEvents ItemInstallerEvents => new DefaultItemInstallerEvents(new BehaviourOptions(InstallMode.Overwrite, MergeMode.Undefined));
		public virtual IFileInstallerEvents FileInstallerEvents => new DefaultFileInstallerEvents(true);
		public virtual bool CustomRequirement()
		{
			return true;
		}
		public abstract List<DescriptorItemRequirements> Requirements { get; }
		public class DescriptorItemRequirements
		{
			public string Database = "master";
			public ID ItemId;
			public List<KeyValuePair<ID, string>> RequiredFields;
		}
	}
}
