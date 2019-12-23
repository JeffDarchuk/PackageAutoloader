# Sitecore Package Autoloader
Automate deployment and installation of packages to the Sitecore initalize pipeline

## Example Usages

+ A default content state:
	* If you have a particular intial set of content that you only want deployed once ever. 
+ Field based integrity check:
	* If you want to validate the value of a particular field to trigger the install.
+ Time based check:
	* Use custom logic to build a time span that the package should be installed within
+ Custom rules for installation:
	* If you have custom business logic that should determine if the package should be installed.


## How it works.
For an example review the PackageAutoLoaderDemo project in this repo
NOTE: This strategy requires the binary be loaded by IIS which may not happen in the absense of configurations demanding the dll be loaded.  See the PackageAutoLoaderDemo project for a simple way to do this.

+ Create the package you wish to install using Sitecore
+ Install the nuget package [here](https://www.nuget.org/packages/PackageAutoLoader/).
+ Add your package to a visual studio project folder
	* Copy/paste your package into an area controlled by a VS project and add it to your csproj
+ In the properties of the package item in visual studio change the build action to "Embedded Resource"
	* Right click -> Properties -> Build Action = "Embedded Resource"
	* This will embed the package into your dll.
+ Add a class extending PackageAutoloaderDescriptor
	* Note the package namespace is the base csproj namespace plus any folders using a '.' delimitor.  For example if you had a project with a root namespace "MyPackageProject" and your zip file was added at /Packages/MyPackage.zip then your package namespace would be "MyPackageProject.Packages.MyPackage.zip"
+ Implement your logic for installation using either the built in item/field validation or custom logic

![main menu](docs/VSproperties.png)

## Descriptor examples

### If an item with a particular ID is missing from Sitecore
```cs
	public class DemoDescriptor : PackageAutoloaderDescriptor
	{
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
```
### If an item with a set particular field value exists, must match all fields
```cs
	public class DemoDescriptor : PackageAutoloaderDescriptor
	{
		public override string PackageNamespace => "PackageAutoloaderDemo.demo.zip";

		public override List<DescriptorItemRequirements> Requirements => new List<DescriptorItemRequirements>()
		{
			new DescriptorItemRequirements()
			{
				Database = "master",
				ItemId = new ID("{76036F5E-CBCE-46D1-AF0A-4143F9B557AA}"),
				RequiredFields = new List<KeyValuePair<ID, string>>()
				{
					new KeyValuePair<ID, string>(new ID("{1036F5E-CBCE-46D1-AF0A-4143F9B529QZ}"), "SOME STUFF"),
					new KeyValuePair<ID, string>(new ID("{413478C3-5C14-4B27-8FA5-C1449BABE71A}"), "SOME OTHER STUFF")
				}
			}
		};
	}
```
### If an item with one of several fields that matches, they don't all need to, just one
```cs
	public class DemoDescriptor : PackageAutoloaderDescriptor
	{
		public override bool AllDescriptorItemRequirementsMustBeValid => false;
		public override string PackageNamespace => "PackageAutoloaderDemo.demo.zip";

		public override List<DescriptorItemRequirements> Requirements => new List<DescriptorItemRequirements>()
		{
			new DescriptorItemRequirements()
			{
				Database = "master",
				ItemId = new ID("{76036F5E-CBCE-46D1-AF0A-4143F9B557AA}"),
				RequiredFields = new List<KeyValuePair<ID, string>>()
				{
					new KeyValuePair<ID, string>(new ID("{1036F5E-CBCE-46D1-AF0A-4143F9B529QZ}"), "SOME STUFF"),
					new KeyValuePair<ID, string>(new ID("{413478C3-5C14-4B27-8FA5-C1449BABE71A}"), "SOME OTHER STUFF")
				}
			}
		};
	}
```
### If the current date is before a specific time
```cs	
public class DemoDescriptor : PackageAutoloaderDescriptor
	{
		public override string PackageNamespace => "PackageAutoloaderDemo.demo.zip";
		public override bool CustomRequirement()
		{
			return DateTime.Now < new DateTime(2020, 1, 22);
		}

		public override List<DescriptorItemRequirements> Requirements => new List<DescriptorItemRequirements>();
	}
```
### If the custom logic passes
```cs
	public class DemoDescriptor : PackageAutoloaderDescriptor
	{
		public override string PackageNamespace => "PackageAutoloaderDemo.demo.zip";
		public override bool CustomRequirement()
		{
			// Any kind of business logic you need
			return Sitecore.Configuration.Settings.RecycleBinActive;
		}

		public override List<DescriptorItemRequirements> Requirements => new List<DescriptorItemRequirements>();
	}
```