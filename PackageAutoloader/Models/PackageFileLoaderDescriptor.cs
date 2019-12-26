using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PackageAutoloader.Models
{
	public abstract class PackageFileLoaderDescriptor : DescriptorBase
	{
		public override string GetRelativeFilePath()
		{
			return RelativeFilePath;
		}

		public abstract string RelativeFilePath { get; }
	}
}
