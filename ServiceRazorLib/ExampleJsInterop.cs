namespace ServiceRazorLib;

using Microsoft.JSInterop;

// This class provides an example of how JavaScript functionality can be wrapped
// in a .NET class for easy consumption. The associated JavaScript module is
// loaded on demand when first needed.
//
// This class can be registered as scoped DI service and then injected into Blazor
// components for use.

public class ExampleJsInterop(IJSRuntime jsRuntime) : IAsyncDisposable {

	private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
			"import", "./_content/ServiceRazorLib/exampleJsInterop.js").AsTask());

	public async ValueTask<string> Prompt(string message) {
		var module = await moduleTask.Value;
		return await module.InvokeAsync<string>("showPrompt", message);
	}

	public async ValueTask DisposeAsync() {
		GC.SuppressFinalize(this);
		if (moduleTask.IsValueCreated) {
			var module = await moduleTask.Value;
			await module.DisposeAsync();
		}
	}

}