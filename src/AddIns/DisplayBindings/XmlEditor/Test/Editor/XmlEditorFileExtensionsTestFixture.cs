﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision: 4723 $</version>
// </file>

using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.Core;
using ICSharpCode.XmlEditor;
using NUnit.Framework;

namespace XmlEditor.Tests.Editor
{
	[TestFixture]
	public class XmlEditorFileExtensionsTestFixture
	{
		AddInTreeNode addinTreeNode;
		
		[SetUp]
		public void SetUp()
		{
			
			string addinXml = "<AddIn name     = 'Xml Editor'\r\n" +
       								"author      = ''\r\n" +
       								"copyright   = 'prj:///doc/copyright.txt'\r\n" +
       								"description = ''\r\n" +
       								"addInManagerHidden = 'preinstalled'>\r\n" +
								"</AddIn>";

			using (StringReader reader = new StringReader(addinXml)) {
				AddIn addin = AddIn.Load(reader);
				
				addinTreeNode = new AddInTreeNode();
				addinTreeNode.Codons.Add(new Codon(addin, "CodeCompletionC#", new Properties(), new ICondition[0]));

				Properties properties = new Properties();
				properties.Set<string>("extensions", " .xml; .xsd ");
				properties.Set<string>("id", "Xml");
				addinTreeNode.Codons.Add(new Codon(addin, "CodeCompletionXml", properties, new ICondition[0]));
			}
		}
		
		[Test]
		public void ExpectedFileExtensions()
		{
			string[] expectedExtension = new string[] { ".xml", ".xsd" };
			Assert.AreEqual(expectedExtension, XmlDisplayBinding.GetXmlFileExtensions(addinTreeNode));
		}
	}
}
