﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop.Dom;
using ICSharpCode.SharpDevelop.Gui;

namespace ICSharpCode.SharpDevelop.Project
{
	class SolutionFolder : ISolutionFolder
	{
		readonly Solution parentSolution;
		readonly Guid idGuid;
		
		public SolutionFolder(Solution parentSolution, Guid idGuid)
		{
			this.parentSolution = parentSolution;
			this.idGuid = idGuid;
			this.items = new SolutionItemsCollection(this);
		}
		
		protected SolutionFolder()
		{
			this.parentSolution = (Solution)this;
			this.items = new SolutionItemsCollection(this);
		}
		
		#region Items Collection
		sealed class SolutionItemsCollection : SimpleModelCollection<ISolutionItem>
		{
			readonly SolutionFolder folder;
			
			public SolutionItemsCollection(SolutionFolder folder)
			{
				this.folder = folder;
			}
			
			protected override void ValidateItem(ISolutionItem item)
			{
				if (item == null)
					throw new ArgumentNullException("item");
				if (item.ParentSolution != folder.parentSolution)
					throw new ArgumentException("The item belongs to a different solution than the folder");
				if (item.ParentFolder != null)
					throw new ArgumentException("The item already has a parent folder");
			}
			
			protected override void OnCollectionChanged(IReadOnlyCollection<ISolutionItem> removedItems, IReadOnlyCollection<ISolutionItem> addedItems)
			{
				foreach (ISolutionItem item in removedItems) {
					folder.parentSolution.ReportRemovedItem(item);
					item.ParentFolder = null;
				}
				foreach (ISolutionItem item in addedItems) {
					item.ParentFolder = folder;
					folder.parentSolution.ReportAddedItem(item);
				}
				base.OnCollectionChanged(removedItems, addedItems);
				folder.parentSolution.IsDirty = true;
			}
		}
		
		readonly SolutionItemsCollection items;
		
		public IMutableModelCollection<ISolutionItem> Items {
			get { return items; }
		}
		#endregion
		
		public string Name { get; set; }
		
		public ISolutionFolder ParentFolder { get; set; }
		
		public ISolution ParentSolution {
			get { return parentSolution; }
		}
		
		public Guid IdGuid {
			get { return idGuid; }
		}
		
		public Guid TypeGuid {
			get { return ProjectTypeGuids.SolutionFolder; }
		}
		
		public bool IsAncestorOf(ISolutionItem item)
		{
			for (ISolutionItem ancestor = item; ancestor != null; ancestor = ancestor.ParentFolder) {
				if (ancestor == this)
					return true;
			}
			return false;
		}
		
		public IProject AddExistingProject(FileName fileName)
		{
			ProjectLoadInformation loadInfo = new ProjectLoadInformation(parentSolution, fileName, fileName.GetFileNameWithoutExtension());
			var descriptor = ProjectBindingService.GetCodonPerProjectFile(fileName);
			if (descriptor != null) {
				loadInfo.TypeGuid = descriptor.Guid;
			}
			IProject project = ProjectBindingService.LoadProject(loadInfo);
			Debug.Assert(project.IdGuid != Guid.Empty);
			this.Items.Add(project);
			return project;
		}
		
		public ISolutionFileItem AddFile(FileName fileName)
		{
			var newItem = new SolutionFileItem(parentSolution);
			newItem.FileName = fileName;
			this.Items.Add(newItem);
			return newItem;
		}
		
		public ISolutionFolder CreateFolder(string name)
		{
			SolutionFolder newFolder = new SolutionFolder(parentSolution, Guid.NewGuid());
			newFolder.Name = name;
			this.Items.Add(newFolder);
			return newFolder;
		}
	}
}
