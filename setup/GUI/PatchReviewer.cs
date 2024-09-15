using System.Collections.Generic;
using Avalonia.Threading;
using DiffPatch;
using PatchReviewer;
using Terraria.ModLoader.Setup.Core.Abstractions;

namespace Setup.GUI.Avalonia;

#if WINDOWS
public class PatchReviewer : IPatchReviewer
{
	public void Show(IReadOnlyCollection<FilePatcher> results, string? commonBasePath = null)
	{
		Dispatcher.UIThread.Invoke(() => new ReviewWindow(results, commonBasePath).ShowDialog());
	}
}
#endif