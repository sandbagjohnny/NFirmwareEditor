﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using JetBrains.Annotations;
using NFirmware;
using NFirmwareEditor.Core;
using NFirmwareEditor.Managers;
using NFirmwareEditor.Models;

namespace NFirmwareEditor.Windows.Tabs
{
	internal partial class PatchesTabPage : UserControl, IEditorTabPage
	{
		private readonly PatchManager m_patchManager;
		private readonly IDictionary<Patch, ListViewItem> m_patchListViewItems = new Dictionary<Patch, ListViewItem>();

		private IEnumerable<Patch> m_allPatches;
		private IEnumerable<Patch> m_suitablePatches;

		private Firmware m_firmware;

		public PatchesTabPage([NotNull] PatchManager patchManager)
		{
			if (patchManager == null) throw new ArgumentNullException("patchManager");

			m_patchManager = patchManager;
			InitializeComponent();

			PatchListView.Resize += (s, e) =>
			{
				NameColumnHeader.Width = PatchListView.Width -
				                         VersionColumnHeader.Width -
				                         InstalledColumnHeader.Width -
				                         CompatibleColumnHeader.Width - 1;
			};
			PatchListView.SelectedIndexChanged += PatchListView_SelectedIndexChanged;
			PatchListView.ItemChecked += PatchListView_ItemChecked;
			ApplyPatchesButton.Click += ApplyPatchesButton_Click;
			RollbackPatchesButton.Click += RollbackPatchesButton_Click;
		}

		[NotNull]
		public IEnumerable<Patch> SelectedPatches
		{
			get { return new List<Patch>((from ListViewItem item in PatchListView.CheckedItems select item.Tag).OfType<Patch>()); }
		}

		[CanBeNull]
		public Patch LastSelectedPatch
		{
			get
			{
				if (PatchListView.SelectedItems.Count == 0) return null;
				return PatchListView.SelectedItems[PatchListView.SelectedItems.Count - 1].Tag as Patch;
			}
		}

		public string Title
		{
			get { return "Patches"; }
		}

		public void Initialize(Configuration configuration)
		{
			m_allPatches = m_patchManager.LoadPatches();
		}

		public void OnActivate()
		{
			DescriptionTextBox.BackColor = Color.White;
			DescriptionTextBox.ReadOnly = true;
		}

		public void OnFirmwareLoaded(Firmware firmware)
		{
			m_firmware = firmware;

			m_suitablePatches = m_allPatches.Where(x => string.Equals(x.Definition, m_firmware.Definition.Name));
			foreach (var patch in m_suitablePatches)
			{
				patch.IsApplied = m_patchManager.IsPatchApplied(patch, m_firmware);
				patch.IsCompatible = m_patchManager.IsPatchCompatible(patch, m_firmware) || patch.IsApplied;
				var item = new ListViewItem(new[]
				{
					patch.Name,
					patch.Version,
					patch.IsApplied ? "Yes" : "No",
					patch.IsCompatible ? "Yes" : "No"
				}) { Tag = patch };

				m_patchListViewItems[patch] = item;
				PatchListView.Items.Add(item);
			}
		}

		public bool OnHotkey(Keys keyData)
		{
			return false;
		}

		public void OnWorkspaceReset()
		{
			m_patchListViewItems.Clear();
			PatchListView.Items.Clear();
		}

		private void UpdatePatchStatuses()
		{
			foreach (var item in m_patchListViewItems)
			{
				var patch = item.Key;
				patch.IsApplied = m_patchManager.IsPatchApplied(patch, m_firmware);
				patch.IsCompatible = m_patchManager.IsPatchCompatible(patch, m_firmware) || patch.IsApplied;

				item.Value.SubItems[2].Text = patch.IsApplied ? "Yes" : "No";
				item.Value.SubItems[3].Text = patch.IsCompatible ? "Yes" : "No";
			}
		}

		private void PatchListView_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (LastSelectedPatch == null) return;

			var sb = new StringBuilder();
			{
				sb.AppendLine("Author: " + LastSelectedPatch.Author);
				sb.AppendLine("Version: " + LastSelectedPatch.Version);
				sb.AppendLine();
				sb.AppendLine(LastSelectedPatch.Description);
			}
			DescriptionTextBox.Text = sb.ToString();
		}

		private void PatchListView_ItemChecked(object sender, ItemCheckedEventArgs e)
		{
			ApplyPatchesButton.Enabled = RollbackPatchesButton.Enabled = PatchListView.CheckedIndices.Count > 0;
		}

		private void ApplyPatchesButton_Click(object sender, EventArgs e)
		{
			if (!SelectedPatches.Any()) return;

			var candidates = SelectedPatches.Where(x => !x.IsApplied).ToList();
			PatchListView.CheckedItems.ForEach(x => x.Checked = false);
			PatchListView.Focus();
			if (!candidates.Any())
			{
				InfoBox.Show("Selected patches were already installed.");
				return;
			}

			var result = m_patchManager.BulkOperation(candidates, p => m_patchManager.ApplyPatch(p, m_firmware));
			UpdatePatchStatuses();

			var sb = new StringBuilder();
			if (result.ProceededPatches.Count > 0)
			{
				sb.AppendLine("Patching is completed.");
				sb.AppendLine();
				sb.AppendLine("List of installed patches:");
				foreach (var patch in result.ProceededPatches)
				{
					sb.AppendLine(" - " + patch.Name);
				}
			}
			if (result.ConflictedPatches.Count > 0)
			{
				if (result.ConflictedPatches.Count == 0)
				{
					sb.AppendLine("Patching is not completed.");
				}
				sb.AppendLine();
				sb.AppendLine("Patches that have not been installed because of conflicts:");
				foreach (var patch in result.ConflictedPatches)
				{
					sb.AppendLine(" - " + patch.Name);
				}
			}
			InfoBox.Show(sb.ToString());
		}
		private void RollbackPatchesButton_Click(object sender, EventArgs e)
		{
			if (!SelectedPatches.Any()) return;

			var candidates = SelectedPatches.Where(x => x.IsApplied).ToList();
			PatchListView.CheckedItems.ForEach(x => x.Checked = false);
			PatchListView.Focus();
			if (!candidates.Any())
			{
				InfoBox.Show("Selected patches are not installed.");
				return;
			}

			var result = m_patchManager.BulkOperation(candidates, p => m_patchManager.RollbackPatch(p, m_firmware));
			UpdatePatchStatuses();

			var sb = new StringBuilder();
			if (result.ProceededPatches.Count > 0)
			{
				sb.AppendLine("Rollback is completed.");
				sb.AppendLine();
				sb.AppendLine("List of rollbacked patches:");
				foreach (var patch in result.ProceededPatches)
				{
					sb.AppendLine(" - " + patch.Name);
				}
			}
			if (result.ConflictedPatches.Count > 0)
			{
				if (result.ConflictedPatches.Count == 0)
				{
					sb.AppendLine("Rollback is not completed.");
				}
				sb.AppendLine();
				sb.AppendLine("Patches that have not been rollbacked because of conflicts:");
				foreach (var patch in result.ConflictedPatches)
				{
					sb.AppendLine(" - " + patch.Name);
				}
			}
			InfoBox.Show(sb.ToString());
		}
	}
}