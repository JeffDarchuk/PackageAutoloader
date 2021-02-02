using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
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
        public bool Async { get; set; } = true;
        public void Process(PipelineArgs args)
        {
            if (Async)
            {
                Task.Run(() =>
                {
                    ProcessDescriptors();
                });
            }
            else
            {
                ProcessDescriptors();
            }
        }
        protected virtual void ProcessDescriptors()
        {
            HashSet<Type> alreadyInstalled = new HashSet<Type>();
            Stack<DescriptorBase> s = new Stack<DescriptorBase>();
            foreach (var descriptor in GetDescriptors())
            {
                try
                {
                    bool shouldInstall = true;
                    if (descriptor.AllItemsExist)
                    {
                        Log.Info(
                            $"PackageAutoLoader: Package {descriptor.GetType().FullName} checking that all items already exist in Sitecore.",
                            this);
                        shouldInstall = false;
                        var archive = new ZipArchive(File.OpenRead(MainUtil.MapPath(descriptor.GetRelativeFilePath())));
                        foreach (ZipArchiveEntry e in archive.Entries.Where(x => x.Name.EndsWith(".zip")))
                        {
                            var package = new ZipArchive(e.Open());
                            foreach (ZipArchiveEntry itemZip in package.Entries.Where(x => x.FullName.StartsWith("items")))
                            {
                                var db = Factory.GetDatabase(itemZip.FullName.Split('/')[1], false);
                                XmlDocument reader = new XmlDocument();
                                reader.Load(itemZip.Open());
                                var id = reader.FirstChild.Attributes["id"].Value;
                                var item = db.DataManager.DataEngine.GetItem(new ID(id), Sitecore.Globalization.Language.Current, Sitecore.Data.Version.Latest);
                                if (item == null)
                                {
                                    shouldInstall = true;
                                    break;
                                }
                            }
                            if (!shouldInstall)
                            {
                                alreadyInstalled.Add(descriptor.GetType());
                                Log.Info(
                                    $"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because it is already installed.",
                                    this);
                            }
                        }
                    }
                    if (shouldInstall && !descriptor.CustomRequirement())
                    {
                        shouldInstall = false;
                        Log.Info(
                            $"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because it doesn't pass a custom requirement",
                            this);
                        alreadyInstalled.Add(descriptor.GetType());
                    }
                    if (shouldInstall)
                    {
                        foreach (var requirement in descriptor.Requirements ?? Enumerable.Empty<DescriptorBase.DescriptorItemRequirements>())
                        {
                            if (descriptor.AllDescriptorItemRequirementsMustBeValid)
                            {
                                shouldInstall = true;
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
                                            shouldInstall = false;
                                            if (descriptor.AllDescriptorItemRequirementsMustBeValid)
                                                break;
                                        }

                                        if (requirement.RequiredFields == null)
                                        {
                                            alreadyInstalled.Add(descriptor.GetType());
                                            Log.Info($"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because the key item already exists",
                                                this);
                                            shouldInstall = false;
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
                                    shouldInstall = false;
                                    if (descriptor.AllDescriptorItemRequirementsMustBeValid)
                                        break;
                                }
                            }
                            else
                            {
                                Log.Info(
                                    $"PackageAutoLoader: Package {descriptor.GetType().FullName} will not be installed because the database is white space on the descriptor, database is a required property.",
                                    this);
                                shouldInstall = false;
                                if (descriptor.AllDescriptorItemRequirementsMustBeValid)
                                    break;
                            }

                            if (descriptor.AllDescriptorItemRequirementsMustBeValid && shouldInstall)
                                break;
                        }
                    }
                    if (!shouldInstall)
                        continue;

                    s.Push(descriptor);
                }catch(Exception e)
                {
                    Log.Error($"Problem analyzing descriptor {descriptor.GetType().FullName}", e, this);
                }
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
                try
                {
                    IProcessingContext context = new SimpleProcessingContext();
                    context.AddAspect(descriptor.ItemInstallerEvents);
                    context.AddAspect(descriptor.FileInstallerEvents);

                    Sitecore.Install.Installer installer = new Sitecore.Install.Installer();
                    installer.InstallPackage(MainUtil.MapPath(descriptor.GetRelativeFilePath()), context);
                }
                catch(Exception e)
                {
                    Log.Warn($"Package installation failed for {descriptor.GetType().FullName}, trying again.", e, this);
                    try
                    {
                        IProcessingContext context = new SimpleProcessingContext();
                        context.AddAspect(descriptor.ItemInstallerEvents);
                        context.AddAspect(descriptor.FileInstallerEvents);

                        Sitecore.Install.Installer installer = new Sitecore.Install.Installer();
                        installer.InstallPackage(MainUtil.MapPath(descriptor.GetRelativeFilePath()), context);
                    }catch(Exception e2)
                    {
                        Log.Error($"Package installation failed for {descriptor.GetType().FullName}.", e2, this);
                    }
                }

            }
        }
    }
}
