﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;
using DiffPatch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Terraria.ModLoader.Setup.Core;
using Terraria.ModLoader.Setup.Core.Abstractions;

namespace Terraria.ModLoader.Setup.GUI
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			string userSettingsFilePath = Path.Combine("setup", "user.settings");
			WorkspaceInfo workspaceInfo = WorkspaceInfo.Initialize();
			if (!File.Exists(userSettingsFilePath)) {
				ProgramSettings programSettings = ProgramSettings.InitializeSettingsFile(userSettingsFilePath);
				MigrateSettings(programSettings, workspaceInfo);
			}

			IConfigurationRoot configuration = new ConfigurationBuilder()
				.AddJsonFile(Path.Combine(Environment.CurrentDirectory, userSettingsFilePath), false, true)
				.Build();

			IServiceCollection services = new ServiceCollection();

			services
				.AddCoreServices(configuration, userSettingsFilePath, workspaceInfo)
				.AddSingleton<ITerrariaExecutableSelectionPrompt, TerrariaExecutableSelectionPrompt>()
				.AddSingleton<IUserPrompt, UserPrompt>()
				.AddSingleton<MainForm>()
				.AddSingleton<IPatchReviewer>(sp => sp.GetRequiredService<MainForm>());

			IServiceProvider serviceProvider = services.BuildServiceProvider();

			workspaceInfo.UpdateGitInfo();

			Application.Run(serviceProvider.GetRequiredService<MainForm>());
		}

		private static void MigrateSettings(ProgramSettings programSettings, WorkspaceInfo workspaceInfo)
		{
			string settingsRootFolder =
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Terraria");

			IEnumerable<string> directories = Directory
				.EnumerateDirectories(settingsRootFolder, "setup_Url_*", SearchOption.TopDirectoryOnly).ToArray();
			FileInfo fileInfo = directories
				.Select(x => new DirectoryInfo(x))
				.SelectMany(x => x.EnumerateFiles("user.config", SearchOption.AllDirectories))
				.MaxBy(x => x?.LastWriteTimeUtc);

			if (fileInfo == null) {
				return;
			}

			XDocument document = XDocument.Load(fileInfo.FullName);

			MigrateDateTimes(document, programSettings);
			MigratePatchMode(document, programSettings);
			MigrateFormatAfterDecompiling(document, programSettings);
			programSettings.Save();

			MigrateWorkspaceInfo(document, workspaceInfo);

			CleanupDirectories(directories, settingsRootFolder);
		}

		private static void MigrateDateTimes(XDocument document, ProgramSettings programSettings)
		{
			Migration<DateTime>[] dateTimeMigrations = [
				new(x => programSettings.TerrariaDiffCutoff = x, "TerrariaDiffCutoff"),
				new(x => programSettings.TerrariaNetCoreDiffCutoff = x, "TerrariaNetCoreDiffCutoff"),
				new(x => programSettings.TModLoaderDiffCutoff = x, "tModLoaderDiffCutoff"),
			];

			foreach (Migration<DateTime> migration in dateTimeMigrations) {
				XElement element = GetElement(document, migration.SettingName);
				if (element != null) {
					migration.UpdateAction(DateTime.Parse(element.Value, CultureInfo.InvariantCulture));
				}
			}
		}

		private static void MigratePatchMode(XDocument document, ProgramSettings programSettings)
		{
			XElement element = GetElement(document, "PatchMode");
			if (element != null) {
				programSettings.PatchMode = (Patcher.Mode)int.Parse(element.Value);
			}
		}

		private static void MigrateFormatAfterDecompiling(XDocument document, ProgramSettings programSettings)
		{
			XElement element = GetElement(document, "FormatAfterDecompiling");
			if (element != null) {
				programSettings.FormatAfterDecompiling = bool.Parse(element.Value);
			}
		}

		private static XElement GetElement(XDocument document, string settingName)
		{
			return document.XPathSelectElement($"//setting[@name='{settingName}']/value");
		}

		private static void CleanupDirectories(IEnumerable<string> directories, string settingsRootFolder)
		{
			try {
				foreach (string directory in directories) {
					Directory.Delete(directory, true);
				}

				if (!Directory.EnumerateFileSystemEntries(settingsRootFolder).Any()) {
					Directory.Delete(settingsRootFolder);
				}
			}
			catch { }
		}

		private static void MigrateWorkspaceInfo(XDocument document, WorkspaceInfo workspaceInfo)
		{
			workspaceInfo.UpdatePaths(
				GetElement(document, "TerrariaSteamDir").Value,
				GetElement(document, "TMLDevSteamDir").Value);
		}

		private sealed record Migration<T>(Action<T> UpdateAction, string SettingName);
	}
}