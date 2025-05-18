// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
//
// SEE Line 132...
//
//

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

public class ApplyJsModules : Task {

	[Required]
	public ITaskItem[] RazorComponents { get; set; } = [];

	[Required]
	public ITaskItem[] RazorGenerate { get; set; } = [];

	[Required]
	public ITaskItem[] JSFileModuleCandidates { get; set; } = [];

	[Output]
	public ITaskItem[] JsFileModules { get; set; } = [];

	public override bool Execute() {
		var razorComponentsWithJsModules = new List<ITaskItem>();
		var razorGenerateWithJsModules = new List<ITaskItem>();
		var unmatchedJsModules = new List<ITaskItem>(this.JSFileModuleCandidates);
		var jsModulesByRazorItem = new Dictionary<string, IList<ITaskItem>>();

		for (var i = 0; i < this.RazorComponents.Length; i++) {
			var componentCandidate = this.RazorComponents[i];
			MatchJsModuleFiles(
				razorComponentsWithJsModules,
				componentCandidate,
				unmatchedJsModules,
				jsModulesByRazorItem,
				"RazorComponent",
				"(.*)\\.razor\\.js$",
				"$1.razor",
				this.Log);
		}

		for (var i = 0; i < this.RazorGenerate.Length; i++) {
			var razorViewCandidate = this.RazorGenerate[i];
			MatchJsModuleFiles(
				razorGenerateWithJsModules,
				razorViewCandidate,
				unmatchedJsModules,
				jsModulesByRazorItem,
				"View",
				"(.*)\\.cshtml\\.js$",
				"$1.cshtml",
				this.Log);
		}

		foreach (var kvp in jsModulesByRazorItem) {
			if (this.RazorComponents.Any(rc => string.Equals(rc.ItemSpec, kvp.Key, StringComparison.OrdinalIgnoreCase))) {
				var component = kvp.Key;
				var jsModuleFiles = kvp.Value;

				if (jsModuleFiles.Count > 1) {
					this.Log.LogError(null, "BLAZOR105", "", component, 0, 0, 0, 0, $"More than one JS module files were found for the razor component '{component}'. " +
						$"Each razor component must have at most a single associated JS module file." +
						Environment.NewLine +
						string.Join(Environment.NewLine, jsModuleFiles.Select(f => f.ItemSpec)));
				}
			} else {
				var view = kvp.Key;
				var jsModuleFiles = kvp.Value;

				if (jsModuleFiles.Count > 1) {
					this.Log.LogError(null, "RZ1007", "", view, 0, 0, 0, 0, $"More than one JS module files were found for the razor view '{view}'. " +
						$"Each razor view must have at most a single associated JS module file." +
						Environment.NewLine +
						string.Join(Environment.NewLine, jsModuleFiles.Select(f => f.ItemSpec)));
				}
			}
		}

		foreach (var unmatched in unmatchedJsModules) {
			this.Log.LogError(null, "CORR106", "", unmatched.ItemSpec, 0, 0, 0, 0, $"The JS module file '{unmatched.ItemSpec}' was defined but no associated razor component or view was found for it.");
		}

		this.JsFileModules = [.. jsModulesByRazorItem.Values.SelectMany(e => e)];

		return !this.Log.HasLoggedErrors;
	}

	private static void MatchJsModuleFiles(
		List<ITaskItem> itemsWithScopes,
		ITaskItem itemCandidate,
		List<ITaskItem> unmatchedJsModules,
		Dictionary<string, IList<ITaskItem>> jsModuleByItem,
		string explicitMetadataName,
		string candidateMatchPattern,
		string replacementExpression,
		TaskLoggingHelper logger) {

		var i = 0;
		while (i < unmatchedJsModules.Count) {
			var jsModuleCandidate = unmatchedJsModules[i];

			var explicitRazorItem = jsModuleCandidate.GetMetadata(explicitMetadataName);

			var jsModuleCandidatePath = jsModuleCandidate.GetMetadata("RelativePath");
			jsModuleCandidatePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
				? jsModuleCandidatePath.Replace('/', '\\')
				: jsModuleCandidatePath.Replace('\\', '/');
			logger.LogMessage(MessageImportance.High, "jsModuleCandidatePath: {0}", jsModuleCandidatePath);

			var razorItem = !string.IsNullOrWhiteSpace(explicitRazorItem) ?
				explicitRazorItem :
				Regex.Replace(jsModuleCandidatePath, candidateMatchPattern, replacementExpression, RegexOptions.IgnoreCase);
			logger.LogMessage(
				MessageImportance.High,
				"razorItem ({0}): {1}",
				string.IsNullOrWhiteSpace(explicitRazorItem) ? "RelativePath" : explicitMetadataName,
				razorItem);

			//
			// DON'T DO THIS!
			// IT ONLY MASKS THE REAL ISSUE AND CAUSES OTHER PROBLEMS
			// BY ALLOWING BAD FILE PATH ITEMS TO CONTINUE
			//
			//if (razorItem.EndsWith(itemCandidate.ItemSpec, true, System.Globalization.CultureInfo.DefaultThreadCurrentCulture)) {
			//
			if (string.Equals(itemCandidate.ItemSpec, razorItem, StringComparison.OrdinalIgnoreCase)) {
				unmatchedJsModules.RemoveAt(i);
				if (!jsModuleByItem.TryGetValue(itemCandidate.ItemSpec, out var existing)) {
					jsModuleByItem[itemCandidate.ItemSpec] = [jsModuleCandidate];
					var item = new TaskItem(itemCandidate);
					item.SetMetadata("JSModule", jsModuleCandidate.GetMetadata("JSModule"));
					itemsWithScopes.Add(item);
				} else {
					existing.Add(jsModuleCandidate);
				}
			} else {
				i++;
			}
		}
	}

}