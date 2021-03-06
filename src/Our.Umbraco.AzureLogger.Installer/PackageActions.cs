﻿namespace Our.Umbraco.AzureLogger.Installer
{
    using System;
    using System.Web;

    using Microsoft.Web.XmlTransform;

    using umbraco.cms.businesslogic.packager.standardPackageActions;
    using umbraco.interfaces;
    using global::Umbraco.Core.Logging;

    public class PackageActions
    {
        public class TransformConfig : IPackageAction
        {
            public string Alias()
            {
                return Constants.TransformConfigAlias;
            }

            public bool Execute(string packageName, System.Xml.XmlNode xmlData)
            {
                return this.Transform(packageName, xmlData);
            }

            public System.Xml.XmlNode SampleXml()
            {
                var str = string.Format("<Action runat=\"install\" undo=\"true\" alias=\"{0}\" file=\"~/{1}\" xdtfile=\"{2}{1}\">" +
                         "</Action>", this.Alias(), Constants.WebConfigFile, Constants.InstallerPath);
                return helper.parseStringToXmlNode(str);
            }

            public bool Undo(string packageName, System.Xml.XmlNode xmlData)
            {
                return this.Transform(packageName, xmlData, true);
            }

            private bool Transform(string packageName, System.Xml.XmlNode xmlData, bool uninstall = false)
            {
                // The config file we want to modify
                if (xmlData.Attributes != null)
                {
                    var file = xmlData.Attributes.GetNamedItem("file").Value;

                    var sourceDocFileName = VirtualPathUtility.ToAbsolute(file);

                    // The xdt file used for tranformation
                    var fileEnd = "install.xdt";
                    if (uninstall)
                    {
                        fileEnd = string.Format("un{0}", fileEnd);
                    }

                    var xdtfile = string.Format("{0}.{1}", xmlData.Attributes.GetNamedItem("xdtfile").Value, fileEnd);
                    var xdtFileName = VirtualPathUtility.ToAbsolute(xdtfile);

                    // The translation at-hand
                    using (var xmlDoc = new XmlTransformableDocument())
                    {
                        xmlDoc.PreserveWhitespace = true;
                        xmlDoc.Load(HttpContext.Current.Server.MapPath(sourceDocFileName));

                        using (var xmlTrans = new XmlTransformation(HttpContext.Current.Server.MapPath(xdtFileName)))
                        {
                            if (xmlTrans.Apply(xmlDoc))
                            {
                                // If we made it here, sourceDoc now has transDoc's changes
                                // applied. So, we're going to save the final result off to
                                // destDoc.
                                try
                                {
                                    xmlDoc.Save(HttpContext.Current.Server.MapPath(sourceDocFileName));
                                }
                                catch (Exception e)
                                {
                                    // Log error message
                                    var message = "Error executing TransformConfig package action (check file write permissions): " + e.Message;
                                    LogHelper.Error(typeof(TransformConfig), message, e);
                                    return false;
                                }
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}
