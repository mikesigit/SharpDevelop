// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Matthew Ward" email="mrward@users.sourceforge.net"/>
//     <version>$Revision$</version>
// </file>

using ICSharpCode.SharpDevelop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using IronPython.Compiler.Ast;

namespace ICSharpCode.PythonBinding
{
	/// <summary>
	/// Visits the code's Python AST and creates a Windows Form.
	/// </summary>
	public class PythonComponentWalker : PythonWalker
	{
		IComponent component;
		PythonControlFieldExpression fieldExpression;
		IComponentCreator componentCreator;
		bool walkingAssignment;
		string componentName = String.Empty;
		PythonCodeDeserializer deserializer;
		ClassDefinition classDefinition;
		
		public PythonComponentWalker(IComponentCreator componentCreator)
		{
			this.componentCreator = componentCreator;
			deserializer = new PythonCodeDeserializer(componentCreator);
		}
		
		/// <summary>
		/// Creates a control either a UserControl or Form from the python code.
		/// </summary>
		public IComponent CreateComponent(string pythonCode)
		{			
			PythonParser parser = new PythonParser();
			PythonAst ast = parser.CreateAst(@"Control.py", new StringTextBuffer(pythonCode));
			ast.Walk(this);
			
			// Did we find the InitializeComponent method?
			if (!FoundInitializeComponentMethod) {
				throw new PythonComponentWalkerException("Unable to find InitializeComponents method.");
			}
			return component;
		}
		
		/// <summary>
		/// Gets the fully qualified name of the base class.
		/// </summary>
		public static string GetBaseClassName(ClassDefinition classDefinition)
		{
			if (classDefinition.Bases.Length > 0) {
				Expression baseClassExpression = classDefinition.Bases[0];
				NameExpression nameExpression = baseClassExpression as NameExpression;
				MemberExpression memberExpression = baseClassExpression as MemberExpression;
				if (nameExpression != null) {
					return nameExpression.Name.ToString();
				}
				return PythonControlFieldExpression.GetMemberName(memberExpression);
			}
			return String.Empty;
		}

		public override bool Walk(ClassDefinition node)
		{
			classDefinition = node;
			componentName = node.Name.ToString();
			if (node.Body != null) {
				node.Body.Walk(this);
			}
			return false;
		}
				
		public override bool Walk(FunctionDefinition node)
		{
			if (IsInitializeComponentMethod(node)) {
				Type type = GetComponentType();
				component = componentCreator.CreateComponent(type, componentName);
				IResourceReader reader = componentCreator.GetResourceReader(CultureInfo.InvariantCulture);
				if (reader != null) {
					reader.Dispose();
				}
				node.Body.Walk(this);
			}
			return false;
		}
		
		public override bool Walk(AssignmentStatement node)
		{			
			if (!FoundInitializeComponentMethod) {
				return false;
			}
			
			if (node.Left.Count > 0) {
				MemberExpression lhsMemberExpression = node.Left[0] as MemberExpression;
				NameExpression lhsNameExpression = node.Left[0] as NameExpression;
				if (lhsMemberExpression != null) {
					fieldExpression = PythonControlFieldExpression.Create(lhsMemberExpression);
					WalkMemberExpressionAssignmentRhs(node.Right);
				} else if (lhsNameExpression != null) {
					CallExpression callExpression = node.Right as CallExpression;
					if (callExpression != null) {
						CreateInstance(lhsNameExpression.Name.ToString(), callExpression);
					}
				}
			}
			return false;
		}
		
		public override bool Walk(ConstantExpression node)
		{
			if (!FoundInitializeComponentMethod) {
				return false;
			}
			
			fieldExpression.SetPropertyValue(componentCreator, node.Value);
			return false;
		}
		
		public override bool Walk(CallExpression node)
		{			
			if (!FoundInitializeComponentMethod) {
				return false;
			}
				
			if (walkingAssignment) {
				WalkAssignmentRhs(node);
			} else {
				WalkMethodCall(node);
			}
			return false;
		}
		
		public override bool Walk(NameExpression node)
		{
			if (!FoundInitializeComponentMethod) {
				return false;
			}
			
			fieldExpression.SetPropertyValue(componentCreator, node.Name.ToString());
			return false;
		}
		
		/// <summary>
		/// Walks a statement of the form:
		/// 
		/// self.a += self.b
		/// </summary>
		public override bool Walk(AugmentedAssignStatement node)
		{
			if (!FoundInitializeComponentMethod) {
				return false;
			}
			
			MemberExpression eventExpression = node.Left as MemberExpression;
			string eventName = eventExpression.Name.ToString();
			PythonControlFieldExpression field = PythonControlFieldExpression.Create(eventExpression);
			
			MemberExpression eventHandlerExpression = node.Right as MemberExpression;
			string eventHandlerName = eventHandlerExpression.Name.ToString();
			
			IComponent currentComponent = fieldExpression.GetObject(componentCreator) as IComponent;
			
			EventDescriptor eventDescriptor = TypeDescriptor.GetEvents(currentComponent).Find(eventName, false);
			PropertyDescriptor propertyDescriptor = componentCreator.GetEventProperty(eventDescriptor);
			propertyDescriptor.SetValue(currentComponent, eventHandlerName);
			return false;
		}		
		
		/// <summary>
		/// Walks the binary expression which is the right hand side of an assignment statement.
		/// </summary>
		void WalkAssignment(BinaryExpression binaryExpression)
		{
			object value = deserializer.Deserialize(binaryExpression);
			fieldExpression.SetPropertyValue(componentCreator, value);
		}

		/// <summary>
		/// Walks the right hand side of an assignment to a member expression.
		/// </summary>
		void WalkMemberExpressionAssignmentRhs(Expression rhs)
		{
			MemberExpression rhsMemberExpression = rhs as MemberExpression;
			if (rhsMemberExpression != null) {
				object propertyValue = GetPropertyValueFromAssignmentRhs(rhsMemberExpression);
				fieldExpression.SetPropertyValue(componentCreator, propertyValue);
			} else {
				walkingAssignment = true;
				BinaryExpression binaryExpression = rhs as BinaryExpression;
				if (binaryExpression != null) {
					WalkAssignment(binaryExpression);
				} else {
					rhs.Walk(this);
				}
				walkingAssignment = false;
			}
		}
		
		static bool IsInitializeComponentMethod(FunctionDefinition node)
		{
			string name = node.Name.ToString().ToLowerInvariant();
			return name == "initializecomponent" || name == "initializecomponents";
		}

		/// <summary>
		/// Adds a component to the list of created objects.
		/// </summary>
		void AddComponent(string name, object obj)
		{
			IComponent component = obj as IComponent;
			if (component != null) {
				string variableName = PythonControlFieldExpression.GetVariableName(name);
				componentCreator.Add(component, variableName);
			}
		}
		
		/// <summary>
		/// Gets the type for the control being walked.
		/// </summary>
		Type GetComponentType()
		{
			string baseClass = GetBaseClassName(classDefinition);
			Type type = componentCreator.GetType(baseClass);
			if (type != null) {
				return type;
			}
			
			if (baseClass.Contains("UserControl")) {
				return typeof(UserControl);
			}
			return typeof(Form);
		}
		
		/// <summary>
		/// Gets the property value from the member expression. The member expression is taken from the
		/// right hand side of an assignment.
		/// </summary>
		object GetPropertyValueFromAssignmentRhs(MemberExpression memberExpression)
		{
			return deserializer.Deserialize(memberExpression);
		}
		
		/// <summary>
		/// Walks the right hand side of an assignment where the assignment expression is a call expression.
		/// Typically the call expression will be a constructor call.
		/// 
		/// Constructor call: System.Windows.Forms.Form()
		/// </summary>
		void WalkAssignmentRhs(CallExpression node)
		{
			MemberExpression memberExpression = node.Target as MemberExpression;
			if (memberExpression != null) {
				string name = fieldExpression.GetInstanceName(componentCreator);
				object instance = CreateInstance(name, node);
				if (instance != null) {
					if (!fieldExpression.SetPropertyValue(componentCreator, instance)) {
						AddComponent(fieldExpression.MemberName, instance);
					}
				} else {
					object obj = deserializer.Deserialize(node);
					if (obj != null) {
						fieldExpression.SetPropertyValue(componentCreator, obj);
					} else if (IsResource(memberExpression)) {
						fieldExpression.SetPropertyValue(componentCreator, GetResource(node));
					} else {
						throw new PythonComponentWalkerException(String.Format("Could not find type '{0}'.", PythonControlFieldExpression.GetMemberName(memberExpression)));
					}
				}
			} else if (node.Target is IndexExpression) {
				WalkArrayAssignmentRhs(node);
			}
		}
		
		/// <summary>
		/// Walks a method call. Typical method calls are:
		/// 
		/// self._menuItem1.Items.AddRange(...)
		/// 
		/// This method will execute the method call.
		/// </summary>
		void WalkMethodCall(CallExpression node)
		{
			if (node.Args.Length == 0) {
				// Ignore method calls with no parameters.
				return;
			}
			
			// Try to get the object being called. Try the form first then
			// look for other controls.
			object member = PythonControlFieldExpression.GetMember(component, node);
			PythonControlFieldExpression field = PythonControlFieldExpression.Create(node);
			if (member == null) {
				member = field.GetMember(componentCreator);
			}
			
			// Execute the method on the object.
			if (member != null) {
				object[] args = deserializer.GetArguments(node).ToArray();
				member.GetType().InvokeMember(field.MethodName, BindingFlags.InvokeMethod, Type.DefaultBinder, member, args);	
			}
		}
		
		/// <summary>
		/// Creates a new instance with the specified name.
		/// </summary>
		object CreateInstance(string name, CallExpression node)
		{
			MemberExpression memberExpression = node.Target as MemberExpression;
			if (memberExpression != null) {
				string typeName = PythonControlFieldExpression.GetMemberName(memberExpression);
				Type type = componentCreator.GetType(typeName);
				if (type != null) {
					if (type.IsAssignableFrom(typeof(ComponentResourceManager))) {
						return componentCreator.CreateInstance(type, new object[0], name, false);
					}
					List<object> args = deserializer.GetArguments(node);
					return componentCreator.CreateInstance(type, args, name, false);
				}
			}
			return null;
		}
		
		bool FoundInitializeComponentMethod {
			get { return component != null; }
		}
		
		/// <summary>
		/// Returns true if the expression is of the form:
		/// 
		/// resources.GetObject(...) or
		/// resources.GetString(...)
		/// </summary>
		bool IsResource(MemberExpression memberExpression)
		{
			string fullName = PythonControlFieldExpression.GetMemberName(memberExpression);
			return fullName.StartsWith("resources.", StringComparison.InvariantCultureIgnoreCase);
		}
		
		object GetResource(CallExpression callExpression)
		{
			IResourceReader reader = componentCreator.GetResourceReader(CultureInfo.InvariantCulture);
			if (reader != null) {
				using (ResourceSet resources = new ResourceSet(reader)) {
					List<object> args = deserializer.GetArguments(callExpression);
					return resources.GetObject(args[0] as String);
				}
			}
			return null;
		}
		
		/// <summary>
		/// Walks the right hand side of an assignment when the assignment is an array creation.
		/// </summary>
		void WalkArrayAssignmentRhs(CallExpression callExpression)
		{
			object array = deserializer.Deserialize(callExpression);
			fieldExpression.SetPropertyValue(componentCreator, array);	
		}
	}
}
