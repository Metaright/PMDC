﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Net;
using System.IO.Compression;
using Mono.Unix;

namespace Updater
{
    class Program
    {
        static string updatefile;
        static string versionfile;
        static List<string> excludedFiles;
        static List<string> executableFiles;
        static Version lastVersion;
        static void Main(string[] args)
        {
            //1: detect platform and load defaults
            updatefile = String.Format("http://127.0.0.1/{0}-x64.zip", GetCurrentPlatform());
            versionfile = "http://127.0.0.1/version";
            lastVersion = new Version(0, 0, 0, 0);
            excludedFiles = new List<string>();
            executableFiles = new List<string>();
            //2: load xml-filename, xml name, last version, exclusions - if possible
            LoadXml();

            try
            {
                Console.WriteLine("Updater Options:");
                Console.WriteLine("1: Force Update");
                Console.WriteLine("2: Uninstall (Retain Save Data)");
                Console.WriteLine("3: Reset Updater XML");
                Console.WriteLine("Press any other key to check for updates.");
                ConsoleKeyInfo choice = Console.ReadKey();
                Console.WriteLine();
                bool force = false;
                if (choice.Key == ConsoleKey.D1)
                    force = true;
                else if (choice.Key == ConsoleKey.D2)
                {
                    Console.WriteLine("Uninstalling...");
                    DeleteWithExclusions("PMDO");
                    DeleteWithExclusions("WaypointServer");
                    DeleteWithExclusions("temp");
                    Console.WriteLine("Done.");
                    Console.ReadKey();
                    return;
                }
                else if (choice.Key == ConsoleKey.D3)
                {
                    Console.WriteLine("Resetting XML");
                    DefaultXml();
                    SaveXml();
                    Console.WriteLine("Done.");
                    Console.ReadKey();
                    return;
                }

                //3: read from site what version is uploaded. if greater than the current version, upgrade
                using (var wc = new WebClient())
                {

                    string uploadedVersionStr = wc.DownloadString(versionfile);
                    Version nextVersion = new Version(uploadedVersionStr);

                    if (lastVersion >= nextVersion)
                    {
                        if (force)
                        {
                            Console.WriteLine("Update will be forced. {0} >= {1}", lastVersion, nextVersion);
                        }
                        else
                        {
                            Console.WriteLine("You are up to date. {0} >= {1}", lastVersion, nextVersion);
                            Console.ReadKey();
                            return;
                        }
                    }

                    Console.WriteLine("Version {0} will be downloaded from {1}.\nPress any key to continue.", nextVersion, updatefile);
                    Console.ReadKey();

                    //4: download the respective zip from specified location
                    if (!Directory.Exists("temp"))
                        Directory.CreateDirectory("temp");
                    string tempFile = Path.Join("temp", Path.GetFileName(updatefile));

                    Console.WriteLine("Downloading from {0} to {1}. May take a while...", updatefile, tempFile);
                    wc.DownloadFile(updatefile, tempFile);

                    //5: unzip and delete by directory - if you want to save your data be sure to make an exception in the xml
                    using (ZipArchive archive = ZipFile.OpenRead(tempFile))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            //go through the list of exemptions
                            bool exempt = false;
                            foreach (string exemption in excludedFiles)
                            {
                                if (exemption.StartsWith(entry.FullName, StringComparison.OrdinalIgnoreCase))
                                {
                                    exempt = true;
                                    break;
                                }
                            }
                            if (!exempt)
                            {
                                bool setPerms = executableFiles.Contains(entry.FullName);
                                string destPath = Path.GetFullPath(Path.Combine(".", entry.FullName));

                                string folderPath = Path.GetDirectoryName(destPath);
                                if (!Directory.Exists(folderPath))
                                    Directory.CreateDirectory(folderPath);
                                if (!destPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                                {
                                    entry.ExtractToFile(destPath, true);
                                    if (setPerms)
                                    {
                                        var info = new UnixFileInfo(destPath);
                                        info.FileAccessPermissions = FileAccessPermissions.AllPermissions;
                                        info.Refresh();
                                    }
                                }
                                else
                                {
                                    if (!Directory.Exists(destPath))
                                        Directory.CreateDirectory(destPath);
                                    Console.WriteLine("Unzipping {0}", entry.FullName);
                                }
                            }
                        }
                    }


                    Console.WriteLine("Cleaning up {0}...", updatefile);
                    File.Delete(tempFile);

                    Console.WriteLine("Incrementing version,", updatefile);
                    lastVersion = nextVersion;

                    //6: create a new xml and save
                    SaveXml();
                    Console.WriteLine("Done.", updatefile);
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
                Console.ReadKey();
            }
        }

        static void LoadXml()
        {
            try
            {
                string path = Path.GetFullPath("Updater.xml");
                if (File.Exists(path))
                {
                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.Load(path);

                    updatefile = xmldoc.SelectSingleNode("Config/UpdateFile").InnerText;
                    versionfile = xmldoc.SelectSingleNode("Config/VersionFile").InnerText;
                    lastVersion = new Version(xmldoc.SelectSingleNode("Config/LastVersion").InnerText);

                    excludedFiles.Clear();
                    XmlNode keys = xmldoc.SelectSingleNode("Config/Exclusions");
                    foreach (XmlNode key in keys.SelectNodes("Exclusion"))
                        excludedFiles.Add(key.InnerText);

                    executableFiles.Clear();
                    XmlNode exes = xmldoc.SelectSingleNode("Config/Executables");
                    foreach (XmlNode key in exes.SelectNodes("Exe"))
                        executableFiles.Add(key.InnerText);
                }
                else
                {
                    DefaultXml();
                    SaveXml();
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
                Console.ReadKey();
            }
        }

        static void DefaultXml()
        {
            updatefile = String.Format("http://127.0.0.1/{0}-x64.zip", GetCurrentPlatform());
            versionfile = "http://127.0.0.1/version";
            lastVersion = new Version(0, 0, 0, 0);
            excludedFiles = new List<string>();
            executableFiles = new List<string>();
            excludedFiles.Clear();
            excludedFiles.Add("PMDO/Config.xml");
            excludedFiles.Add("PMDO/Keyboard.xml");
            excludedFiles.Add("PMDO/Gamepad.xml");
            excludedFiles.Add("PMDO/Contacts.xml");
            excludedFiles.Add("PMDO/LOG/");
            excludedFiles.Add("PMDO/MODS/");
            excludedFiles.Add("PMDO/REPLAY/");
            excludedFiles.Add("PMDO/RESCUE/");
            excludedFiles.Add("PMDO/SAVE/");
            executableFiles.Clear();
            executableFiles.Add("PMDO/PMDO");
            executableFiles.Add("PMDO/dev.sh");
            executableFiles.Add("PMDO/MapGenTest");
            executableFiles.Add("WaypointServer/WaypointServer");
            executableFiles.Add("WaypointServer.app/Contents/MacOS/WaypointServer");
        }

        static void SaveXml()
        {
            {
                XmlDocument xmldoc = new XmlDocument();

                XmlNode docNode = xmldoc.CreateElement("Config");
                xmldoc.AppendChild(docNode);

                appendConfigNode(xmldoc, docNode, "UpdateFile", updatefile);
                appendConfigNode(xmldoc, docNode, "VersionFile", versionfile);
                appendConfigNode(xmldoc, docNode, "LastVersion", lastVersion.ToString());

                XmlNode keys = xmldoc.CreateElement("Exclusions");
                foreach (string key in excludedFiles)
                {
                    XmlNode node = xmldoc.CreateElement("Exclusion");
                    node.InnerText = key;
                    keys.AppendChild(node);
                }
                docNode.AppendChild(keys);

                XmlNode exes = xmldoc.CreateElement("Executables");
                foreach (string key in executableFiles)
                {
                    XmlNode node = xmldoc.CreateElement("Exe");
                    node.InnerText = key;
                    exes.AppendChild(node);
                }
                docNode.AppendChild(exes);

                xmldoc.Save("Updater.xml");
            }
        }

        static bool DeleteWithExclusions(string path)
        {
            if (isExcluded(path))
                return false;

            bool deletedAll = true;
            string[] listDir = Directory.GetDirectories(path);
            foreach (string dir in listDir)
            {
                bool deletedAllSub = DeleteWithExclusions(dir);

                if (deletedAllSub)
                    Directory.Delete(dir, false);
                else
                    deletedAll = false;
            }
            string[] listFiles = Directory.GetFiles(path);
            foreach (string file in listFiles)
            {
                if (!isExcluded(file))
                    File.Delete(file);
                else
                    deletedAll = false;
            }
            return deletedAll;
        }

        static bool isExcluded(string path)
        {
            string fullPath = Path.GetFullPath(path).Replace("\\", "/").Trim('/');
            foreach (string exclusion in excludedFiles)
            {
                string fullExclusion = Path.GetFullPath(exclusion).Replace("\\", "/").Trim('/');
                if (string.Equals(fullPath, fullExclusion, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetCurrentPlatform()
        {
            string[] platformNames = new string[]
            {
            "LINUX",
            "OSX",
            "WINDOWS",
            "FREEBSD",
            "NETBSD",
            "OPENBSD"
            };

            for (int i = 0; i < platformNames.Length; i += 1)
            {
                OSPlatform platform = OSPlatform.Create(platformNames[i]);
                if (RuntimeInformation.IsOSPlatform(platform))
                {
                    return platformNames[i].ToLowerInvariant();
                }
            }

            return "unknown";
        }

        private static void appendConfigNode(XmlDocument doc, XmlNode parentNode, string name, string text)
        {
            XmlNode node = doc.CreateElement(name);
            node.InnerText = text;
            parentNode.AppendChild(node);
        }
    }
}
