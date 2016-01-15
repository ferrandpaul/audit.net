﻿#region License
// Copyright (c) 2015-2016, Vör Security Ltd.
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of Vör Security, OSS Index, nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL VÖR SECURITY BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

//using EnvDTE;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using NuGet.VisualStudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using System.Threading;
using System.Windows.Forms;

namespace NugetAuditor.VSIX
{
    internal static class VsUtility
    {
        internal const string UnloadedProjectTypeGuid = "{67294A52-A4F0-11D2-AA88-00C04F688DDE}";

        internal static IVsHierarchy GetProjectHierarchy(Project project)
        {
            IVsHierarchy hierarchy = null;

            var solution = ServiceLocator.GetInstance<IVsSolution>();

            ErrorHandler.ThrowOnFailure(solution.GetProjectOfUniqueName(project.UniqueName, out hierarchy));

            solution = null;

            return hierarchy;
        }

        internal static IVsTextLines GetDocumentTextLines(string path)
        {
            IVsUIHierarchy uiHierarchy;
            uint itemID;
            IVsWindowFrame windowFrame;

            if (VsShellUtilities.IsDocumentOpen(VSPackage.Instance, path, new Guid(LogicalViewID.TextView), out uiHierarchy, out itemID, out windowFrame))
            {
                IVsTextView pView;
                IVsTextLines textLines;

                pView = VsShellUtilities.GetTextView(windowFrame);

                ErrorHandler.ThrowOnFailure(pView.GetBuffer(out textLines));

                return textLines;
            }

            return null;
        }
           
        internal static Project GetActiveProject(IVsMonitorSelection vsMonitorSelection)
        {
            Project project;
            IntPtr zero = IntPtr.Zero;
            IntPtr ppSC = IntPtr.Zero;
            try
            {
                uint num;
                IVsMultiItemSelect select;
                object obj2;
                vsMonitorSelection.GetCurrentSelection(out zero, out num, out select, out ppSC);
                if (zero == IntPtr.Zero)
                {
                    return null;
                }
                if (num == 0xfffffffd)
                {
                    return null;
                }
                IVsHierarchy typedObjectForIUnknown = Marshal.GetTypedObjectForIUnknown(zero, typeof(IVsHierarchy)) as IVsHierarchy;
                if ((typedObjectForIUnknown != null) && (typedObjectForIUnknown.GetProperty(0xfffffffe, -2027, out obj2) >= 0))
                {
                    return (Project)obj2;
                }
                project = null;
            }
            finally
            {
                if (zero != IntPtr.Zero)
                {
                    Marshal.Release(zero);
                }
                if (ppSC != IntPtr.Zero)
                {
                    Marshal.Release(ppSC);
                }
            }
            return project;
        }

        internal static bool IsProjectSupported(Project project)
        {
            try
            {
                ServiceLocator.GetInstance<IVsPackageInstallerServices>().IsPackageInstalled(project, "__dummy__");

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        internal static bool IsProjectUnloaded(this Project project)
        {
            return UnloadedProjectTypeGuid.Equals(project.Kind, StringComparison.OrdinalIgnoreCase);
        }

        internal static IEnumerable<Project> GetAllSupportedProjects(Solution solution)
        {
            if (solution == null || !solution.IsOpen)
            {
                yield break;
            }

            Stack<Project> source = new Stack<Project>();

            foreach (Project project in solution.Projects)
            {
                source.Push(project);
            }

            while (source.Any())
            {
                Project project = source.Pop();

                if (IsProjectSupported(project))
                {
                    yield return project;
                }

                ProjectItems projectItems = null;

                try
                {
                    projectItems = project.ProjectItems;
                }
                catch (NotImplementedException)
                {
                    continue;
                }

                foreach (ProjectItem projectItem in projectItems)
                {
                    try
                    {
                        if (projectItem.SubProject != null)
                        {
                            source.Push(projectItem.SubProject);
                        }
                    }
                    catch (NotImplementedException)
                    {
                    }
                }
            }
        }

        internal static string GetSolutionName(Solution solution)
        {
            return (string)solution.Properties.Item("Name").Value;
        }
    }
}