// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Windows.Forms;
using ICSharpCode.PythonBinding;
using NUnit.Framework;
using PythonBinding.Tests.Utils;

namespace PythonBinding.Tests.Designer
{
	/// <summary>
	/// Tests that a custom collection class generates the correct code.
	/// The collection class should be a property of a custom component or user control
	/// and it should be marked with DesignerSerializationVisibility.Content.
	/// </summary>
	[TestFixture]
	public class GenerateCustomCollectionItemsTestFixture
	{
		string generatedPythonCode;
		
		[TestFixtureSetUp]
		public void SetUpFixture()
		{
			using (DesignSurface designSurface = new DesignSurface(typeof(Form))) {
				IDesignerHost host = (IDesignerHost)designSurface.GetService(typeof(IDesignerHost));
				IEventBindingService eventBindingService = new MockEventBindingService(host);
				Form form = (Form)host.RootComponent;
				form.ClientSize = new Size(200, 300);

				PropertyDescriptorCollection descriptors = TypeDescriptor.GetProperties(form);
				PropertyDescriptor namePropertyDescriptor = descriptors.Find("Name", false);
				namePropertyDescriptor.SetValue(form, "MainForm");
				
				// Add custom control
				CustomUserControl userControl = (CustomUserControl)host.CreateComponent(typeof(CustomUserControl), "userControl1");
				userControl.Location = new Point(0, 0);
				userControl.ClientSize = new Size(200, 100);
				userControl.FooItems.Add(new FooItem("aa"));
				userControl.FooItems.Add(new FooItem("bb"));
				userControl.ParentComponent.ParentBarItems.Add(new BarItem("cc"));
				userControl.ParentComponent.ParentBarItems.Add(new BarItem("dd"));
				form.Controls.Add(userControl);
				
				PythonControl pythonForm = new PythonControl("    ");
				generatedPythonCode = pythonForm.GenerateInitializeComponentMethod(form);				
			}
		}
		
		[Test]
		public void GeneratedCode()
		{
			string expectedCode = "def InitializeComponent(self):\r\n" +
								"    fooItem1 = PythonBinding.Tests.Utils.FooItem()\r\n" +
								"    fooItem2 = PythonBinding.Tests.Utils.FooItem()\r\n" +
								"    barItem1 = PythonBinding.Tests.Utils.BarItem()\r\n" +
								"    barItem2 = PythonBinding.Tests.Utils.BarItem()\r\n" +
								"    self._userControl1 = PythonBinding.Tests.Utils.CustomUserControl()\r\n" +
								"    self.SuspendLayout()\r\n" +
								"    # \r\n" +
								"    # userControl1\r\n" +
								"    # \r\n" +
								"    fooItem1.Text = \"aa\"\r\n" +
								"    fooItem2.Text = \"bb\"\r\n" +
								"    barItem1.Text = \"cc\"\r\n" +
								"    barItem2.Text = \"dd\"\r\n" +
								"    self._userControl1.FooItems.AddRange(System.Array[PythonBinding.Tests.Utils.FooItem](\r\n" +
								"        [fooItem1,\r\n" +
								"        fooItem2]))\r\n" +
								"    self._userControl1.Location = System.Drawing.Point(0, 0)\r\n" +
								"    self._userControl1.Name = \"userControl1\"\r\n" +
								"    self._userControl1.ParentComponent.ParentBarItems.AddRange(System.Array[PythonBinding.Tests.Utils.BarItem](\r\n" +
								"        [barItem1,\r\n" +
								"        barItem2]))\r\n" +
								"    self._userControl1.Size = System.Drawing.Size(200, 100)\r\n" +
								"    self._userControl1.TabIndex = 0\r\n" +
								"    # \r\n" +
								"    # MainForm\r\n" +
								"    # \r\n" +
								"    self.ClientSize = System.Drawing.Size(200, 300)\r\n" +
								"    self.Controls.Add(self._userControl1)\r\n" +
								"    self.Name = \"MainForm\"\r\n" +
								"    self.ResumeLayout(False)\r\n" +
								"    self.PerformLayout()\r\n";
			
			Assert.AreEqual(expectedCode, generatedPythonCode, generatedPythonCode);
		}
	}
}
