using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.ComponentModel;
using System.Reflection;

namespace EberoNest;

internal sealed class SettingsForm : EberoNestForm
{
	private sealed class ProductNameControl : Control
	{
		private const TextFormatFlags TextFlags = TextFormatFlags.NoClipping | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

		internal ProductNameControl()
		{
			Font = new Font("Segoe UI", 21f, FontStyle.Bold, GraphicsUnit.Point);
			AutoSize = true;
			BackColor = Canvas;
			Margin = Padding.Empty;
			AccessibleName = "EberoNest";
			AccessibleRole = AccessibleRole.Text;
			SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		}

		public override Size GetPreferredSize(Size proposedSize)
		{
			Size size = TextRenderer.MeasureText("Ebero", Font, Size.Empty, TextFormatFlags.NoClipping | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			Size size2 = TextRenderer.MeasureText("Nest", Font, Size.Empty, TextFormatFlags.NoClipping | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			return new Size(size.Width + size2.Width, Math.Max(size.Height, size2.Height));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Size size = TextRenderer.MeasureText("Ebero", Font, Size.Empty, TextFormatFlags.NoClipping | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			int y = Math.Max(0, (ClientSize.Height - size.Height) / 2);
			TextRenderer.DrawText(e.Graphics, "Ebero", Font, new Point(0, y), PrimaryText, TextFormatFlags.NoClipping | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(e.Graphics, "Nest", Font, new Point(size.Width, y), Accent, TextFormatFlags.NoClipping | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}
	}

	private enum ButtonKind
	{
		Primary,
		Neutral,
		Danger
	}

	internal static readonly Color Canvas = Color.FromArgb(24, 26, 31);

	private static readonly Color Surface = Color.FromArgb(32, 35, 41);

	internal static readonly Color Elevated = Color.FromArgb(48, 52, 59);

	internal static readonly Color Divider = Color.FromArgb(43, 47, 55);

	private static readonly Color PrimaryText = Color.FromArgb(236, 238, 242);

	private static readonly Color SecondaryText = Color.FromArgb(166, 172, 182);

	internal static readonly Color Accent = Color.FromArgb(24, 112, 232);

	private static readonly Color Danger = Color.FromArgb(237, 76, 90);

	private static readonly Color Success = Color.FromArgb(46, 204, 113);

	private readonly Organizer organizer;

	private readonly Func<IWin32Window?, Task> organize;

	private readonly RulesListControl rulesList = new RulesListControl();

	private readonly Label status = new Label();

	private readonly ToggleSwitch startupSwitch = new ToggleSwitch();

	private PictureBox? headerLogo;

	private readonly IStartupManager startupManager;

	private readonly IUpdateNotificationService updateNotifications;

	private readonly IReleaseBrowser releaseBrowser;

	private readonly LinkLabel updateNotice = new LinkLabel();

	private TableLayoutPanel? aboutArea;

	private bool restoringStartupSwitch;

	internal SettingsForm(Organizer organizer, Func<IWin32Window?, Task> organize, IStartupManager startupManager,
		IUpdateNotificationService updateNotifications, IReleaseBrowser releaseBrowser)
	{
		this.organizer = organizer;
		this.organize = organize;
		this.startupManager = startupManager;
		this.updateNotifications = updateNotifications;
		this.releaseBrowser = releaseBrowser;
		Text = AppLocalization.Get("app.name");
		StartPosition = FormStartPosition.CenterScreen;
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		MinimumSize = new Size(860, 640);
		ClientSize = new Size(1000, 740);
		BackColor = Canvas;
		ForeColor = PrimaryText;
		Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(22),
			ColumnCount = 1,
			RowCount = 7,
			BackColor = Canvas
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76f));
		Controls.Add(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(BuildHeader(), 0, 0);
		tableLayoutPanel.Controls.Add(MakeDivider(), 0, 1);
		tableLayoutPanel.Controls.Add(BuildSectionHeading(), 0, 2);
		ConfigureRulesList();
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			Padding = Padding.Empty,
			BackColor = Canvas
		};
		panel.Controls.Add(rulesList);
		tableLayoutPanel.Controls.Add(panel, 0, 3);
		tableLayoutPanel.Controls.Add(BuildActions(), 0, 4);
		tableLayoutPanel.Controls.Add(MakeDivider(), 0, 5);
		tableLayoutPanel.Controls.Add(BuildFooter(), 0, 6);
		startupSwitch.Checked = startupManager.IsEnabled;
		startupSwitch.CheckedChanged += StartupSwitch_CheckedChanged;
		FormClosed += (object? _, FormClosedEventArgs _) =>
		{
			updateNotifications.CheckCompleted -= UpdateNotifications_CheckCompleted;
			AppLocalization.LanguageChanged -= Localization_LanguageChanged;
			headerLogo?.Image?.Dispose();
		};
		AppLocalization.LanguageChanged += Localization_LanguageChanged;
		updateNotifications.CheckCompleted += UpdateNotifications_CheckCompleted;
		AppLocalization.Apply(this);
		ApplyUpdateNotice(updateNotifications.LastSuccessfulResult);
		ReloadGrid();
	}

	private Control BuildHeader()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			BackColor = Canvas,
			Margin = Padding.Empty,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Absolute, 70f),
				new ColumnStyle(SizeType.Percent, 100f)
			}
		};
		headerLogo = new PictureBox
		{
			Image = LoadLogoImageForAbout(),
			SizeMode = PictureBoxSizeMode.Zoom,
			Size = new Size(54, 54),
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0)
		};
		tableLayoutPanel.Controls.Add(headerLogo, 0, 0);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			AutoSize = true,
			Anchor = AnchorStyles.Left,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			BackColor = Canvas,
			Padding = Padding.Empty,
			Margin = Padding.Empty
		};
		flowLayoutPanel.Controls.Add(new ProductNameControl());
		Label tagline = new Label
		{
			Text = AppLocalization.Get("app.tagline"),
			AutoSize = true,
			ForeColor = SecondaryText,
			Font = new Font("Segoe UI", 10.5f),
			Margin = new Padding(1, 1, 0, 0)
		};
		AppLocalization.Bind(tagline, "app.tagline");
		flowLayoutPanel.Controls.Add(tagline);
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 1, 0);
		return tableLayoutPanel;
	}

	private Control BuildSectionHeading()
	{
		TableLayoutPanel panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 2,
			BackColor = Canvas,
			Margin = Padding.Empty,
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 38f),
				new RowStyle(SizeType.Percent, 100f)
			},
		};
		Label heading = new Label
		{
			Text = AppLocalization.Get("settings.heading"), Dock = DockStyle.Fill,
			Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = PrimaryText,
			TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
		};
		Label description = new Label
		{
			Text = AppLocalization.Get("settings.description"), Dock = DockStyle.Fill,
			ForeColor = SecondaryText, Font = Font, TextAlign = ContentAlignment.MiddleLeft,
			Margin = Padding.Empty
		};
		AppLocalization.Bind(heading, "settings.heading");
		AppLocalization.Bind(description, "settings.description");
		panel.Controls.Add(heading, 0, 0);
		panel.Controls.Add(description, 0, 1);
		return panel;
	}

	private Control BuildActions()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			BackColor = Canvas,
			Margin = Padding.Empty,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 174f)
			}
		};
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			BackColor = Canvas,
			Padding = new Padding(0, 6, 0, 0),
			Margin = Padding.Empty
		};
		Button newCategory = MakeButton(AppLocalization.Get("button.newCategory"), (object? _, EventArgs _) =>
		{
			AddRule();
		}, ButtonKind.Primary);
		AppLocalization.Bind(newCategory, "button.newCategory");
		flowLayoutPanel.Controls.Add(newCategory);
		Button edit = MakeButton(AppLocalization.Get("button.edit"), (object? _, EventArgs _) =>
		{
			EditSelected();
		}, ButtonKind.Neutral);
		AppLocalization.Bind(edit, "button.edit");
		flowLayoutPanel.Controls.Add(edit);
		Button delete = MakeButton(AppLocalization.Get("button.delete"), (object? _, EventArgs _) =>
		{
			DeleteSelected();
		}, ButtonKind.Danger);
		AppLocalization.Bind(delete, "button.delete");
		flowLayoutPanel.Controls.Add(delete);
		Button button = MakeButton(AppLocalization.Get("button.organizeNow"), async (object? _, EventArgs _) =>
		{
			await organize(this);
		}, ButtonKind.Primary);
		button.Dock = DockStyle.None;
		button.Size = new Size(174, 38);
		button.Anchor = AnchorStyles.Right;
		button.Margin = Padding.Empty;
		AppLocalization.Bind(button, "button.organizeNow");
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft,
			WrapContents = false,
			BackColor = Canvas,
			Padding = new Padding(0, 6, 0, 0),
			Margin = Padding.Empty
		};
		flowLayoutPanel2.Controls.Add(button);
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
		tableLayoutPanel.Controls.Add(flowLayoutPanel2, 1, 0);
		return tableLayoutPanel;
	}

	private Control BuildFooter()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 3,
			RowCount = 1,
			BackColor = Canvas,
			Margin = Padding.Empty,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 43f),
				new ColumnStyle(SizeType.Percent, 33f),
				new ColumnStyle(SizeType.Percent, 24f)
			}
		};
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			BackColor = Canvas,
			Padding = new Padding(0, 11, 8, 0),
			Margin = Padding.Empty
		};
		startupSwitch.Size = new Size(46, 26);
		startupSwitch.Margin = new Padding(0, 5, 12, 0);
		startupSwitch.AccessibleName = AppLocalization.Get("startup.title");
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			AutoSize = true,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			BackColor = Canvas,
			Margin = Padding.Empty
		};
		Label startupTitle = new Label
		{
			Text = AppLocalization.Get("startup.title"),
			AutoSize = true,
			ForeColor = PrimaryText,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold),
			Margin = Padding.Empty
		};
		AppLocalization.Bind(startupTitle, "startup.title");
		flowLayoutPanel2.Controls.Add(startupTitle);
		Label startupDescription = new Label
		{
			Text = AppLocalization.Get("startup.description"),
			AutoSize = true,
			ForeColor = SecondaryText,
			Font = new Font("Segoe UI", 8.5f),
			Margin = new Padding(0, 3, 0, 0)
		};
		AppLocalization.Bind(startupDescription, "startup.description");
		flowLayoutPanel2.Controls.Add(startupDescription);
		flowLayoutPanel.Controls.Add(startupSwitch);
		flowLayoutPanel.Controls.Add(flowLayoutPanel2);
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 2,
			BackColor = Canvas,
			Padding = new Padding(20, 10, 4, 0),
			Margin = Padding.Empty
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		Panel statusDot = new Panel
		{
			BackColor = Canvas,
			Size = new Size(12, 12),
			Anchor = AnchorStyles.Left,
			Margin = new Padding(0, 6, 0, 0)
		};
		statusDot.Paint += (object? _, PaintEventArgs e) =>
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using SolidBrush brush = new SolidBrush(Success);
			e.Graphics.FillEllipse(brush, 0, 0, statusDot.Width - 1, statusDot.Height - 1);
		};
		tableLayoutPanel2.Controls.Add(statusDot, 0, 0);
		Label organizerLabel = new Label
		{
			Text = AppLocalization.Get("organizer.active"),
			Dock = DockStyle.Fill,
			ForeColor = PrimaryText,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = Padding.Empty
		};
		AppLocalization.Bind(organizerLabel, "organizer.active");
		tableLayoutPanel2.Controls.Add(organizerLabel, 1, 0);
		AppLocalization.Bind(status, "organizer.monitoring");
		status.Dock = DockStyle.Fill;
		status.ForeColor = SecondaryText;
		status.Font = new Font("Segoe UI", 8.5f);
		status.TextAlign = ContentAlignment.TopLeft;
		tableLayoutPanel2.Controls.Add(status, 1, 1);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 1, 0);
	Button aboutButton = MakeButton(AppLocalization.Get("about.button", AppVersion.Display), (object? _, EventArgs _) =>
		{
			ShowAbout();
		}, ButtonKind.Neutral);
		aboutButton.Dock = DockStyle.None;
		aboutButton.Size = new Size(172, 38);
		aboutButton.Anchor = AnchorStyles.Right;
		aboutButton.MinimumSize = new Size(172, 38);
		aboutButton.Margin = Padding.Empty;
		AppLocalization.Bind(aboutButton, "about.button", AppVersion.Display);
		updateNotice.Text = string.Empty;
		updateNotice.AutoSize = false;
		updateNotice.Size = new Size(196, 20);
		updateNotice.Anchor = AnchorStyles.Right;
		updateNotice.TextAlign = ContentAlignment.MiddleRight;
		updateNotice.BackColor = Canvas;
		updateNotice.ForeColor = Accent;
		updateNotice.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
		updateNotice.LinkColor = Accent;
		updateNotice.ActiveLinkColor = Accent;
		updateNotice.VisitedLinkColor = Accent;
		updateNotice.LinkBehavior = LinkBehavior.HoverUnderline;
		updateNotice.Visible = false;
		updateNotice.Margin = Padding.Empty;
		updateNotice.LinkClicked += (_, _) => OpenAvailableUpdate();
		aboutArea = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			BackColor = Canvas,
			Padding = Padding.Empty,
			Margin = Padding.Empty,
			ColumnStyles =
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 172f)
			}
		};
		updateNotice.Dock = DockStyle.Fill;
		aboutArea.Controls.Add(updateNotice, 0, 0);
		aboutArea.Controls.Add(aboutButton, 1, 0);
		tableLayoutPanel.Controls.Add(aboutArea, 2, 0);
		return tableLayoutPanel;
	}

		internal RulesListControl RulesListForSmoke => rulesList;

	internal LinkLabel UpdateNoticeForSmoke => updateNotice;

	internal Button AboutButtonForSmoke => aboutArea?.Controls.OfType<Button>().Single()
		?? throw new InvalidOperationException("About button has not been constructed.");

	internal TableLayoutPanel AboutAreaForSmoke => aboutArea ?? throw new InvalidOperationException("About footer has not been constructed.");

	private void ConfigureRulesList()
	{
		rulesList.Dock = DockStyle.Fill;
		rulesList.ItemDoubleClick += (object? _, EventArgs _) => EditSelected();
	}

	private void ReloadGrid()
	{
		rulesList.SetRules(organizer.Rules.OrderBy((RuleCategory x) => x.Name, StringComparer.CurrentCultureIgnoreCase));
	}

private void AddRule()
	{
		RuleEditorForm ruleEditorForm = new RuleEditorForm(null, organizer.Rules)
		{
			Icon = Icon
		};
		if (ruleEditorForm.ShowDialog(this) != DialogResult.OK || ruleEditorForm.Result == null)
		{
			return;
		}
		IReadOnlyList<RuleCategory> rules = organizer.Rules;
		int num = 0;
		RuleCategory[] array = new RuleCategory[1 + rules.Count];
		foreach (RuleCategory item in rules)
		{
			array[num] = item;
			num++;
		}
		array[num] = ruleEditorForm.Result;
		ApplyChanges(array);
	}

	private void EditSelected()
	{
		RuleCategory? selected = SelectedRule();
		if (selected == null)
		{
			return;
		}
		RuleEditorForm editor = new RuleEditorForm(selected, organizer.Rules)
		{
			Icon = Icon
		};
		if (editor.ShowDialog(this) == DialogResult.OK && editor.Result != null)
		{
			List<RuleCategory> updated = organizer.Rules.Select((RuleCategory rule) => (!rule.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase)) ? rule : editor.Result).ToList();
			ApplyChanges(updated);
		}
	}

	private void DeleteSelected()
	{
		RuleCategory? selected = SelectedRule();
		if (selected == null)
		{
			return;
		}
		if (selected.IsBuiltIn)
		{
			MessageBox.Show(this, AppLocalization.Get("rules.delete.builtin"), AppLocalization.Get("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else if (MessageBox.Show(this, AppLocalization.Get("rules.delete.confirm", selected.Name), AppLocalization.Get("rules.delete.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
		{
			ApplyChanges(organizer.Rules.Where((RuleCategory rule) => !rule.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase)));
		}
	}

		private RuleCategory? SelectedRule()
	{
		RuleCategory? selected = rulesList.SelectedRule;
		return selected is null ? null : organizer.Rules.FirstOrDefault((RuleCategory rule) => rule.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase));
	}

private void ApplyChanges(IEnumerable<RuleCategory> updated)
	{
		try
		{
			List<RuleCategory> list = RulesStore.NormalizeAndValidate(updated);
			RulesStore.Save(list);
			organizer.UpdateRules(list);
			ReloadGrid();
			AppLocalization.Bind(status, "rules.saved");
		}
		catch (Exception ex) when ((ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) ? true : false)
		{
			MessageBox.Show(this, AppLocalization.LocalizeRuleError(ex), AppLocalization.Get("rules.save.failure.title"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		}
	}

	private void StartupSwitch_CheckedChanged(object? sender, EventArgs e)
	{
		if (restoringStartupSwitch)
		{
			return;
		}
		try
		{
			if (startupSwitch.Checked)
			{
				startupManager.Enable();
			}
			else
			{
				startupManager.Disable();
			}
			RestoreStartupSwitch(startupManager.IsEnabled);
		}
		catch (Exception ex)
		{
			bool enabled;
			try
			{
				enabled = startupManager.IsEnabled;
			}
			catch
			{
				enabled = false;
			}
			RestoreStartupSwitch(enabled);
			string detail = AppLocalization.TranslateKnownOperationalMessage(ex.Message);
			MessageBox.Show(this, AppLocalization.Get("startup.error.body") + "\n" + detail,
				AppLocalization.Get("startup.error.title"), MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void RestoreStartupSwitch(bool enabled)
	{
		restoringStartupSwitch = true;
		try
		{
			startupSwitch.Checked = enabled;
		}
		finally
		{
			restoringStartupSwitch = false;
		}
	}

	private void UpdateNotifications_CheckCompleted(UpdateCheckResult result)
	{
		if (IsDisposed || !IsHandleCreated) return;
		if (InvokeRequired)
		{
			try { BeginInvoke(new Action(() => ApplyUpdateNotice(result))); }
			catch (InvalidOperationException) { }
			return;
		}
		ApplyUpdateNotice(result);
	}

	private void ApplyUpdateNotice(UpdateCheckResult? result)
	{
		OfficialRelease? release = result?.Status == UpdateCheckStatus.UpdateAvailable ? result.LatestRelease : null;
		updateNotice.Tag = release;
		updateNotice.Text = release is null ? string.Empty : AppLocalization.Get("updates.notice", release.Tag);
		updateNotice.Visible = release is not null && OfficialReleaseUrl.TryValidate(release.Url, release.Version);
	}

	private void Localization_LanguageChanged(object? sender, EventArgs e)
	{
		if (IsDisposed) return;
		if (InvokeRequired)
		{
			try { BeginInvoke(new Action(() => Localization_LanguageChanged(sender, e))); }
			catch (InvalidOperationException) { }
			return;
		}
		AppLocalization.Apply(this);
		startupSwitch.AccessibleName = AppLocalization.Get("startup.title");
		rulesList.Invalidate();
		ApplyUpdateNotice(updateNotifications.LastSuccessfulResult);
	}

	private void OpenAvailableUpdate()
	{
		if (updateNotice.Tag is OfficialRelease release && OfficialReleaseUrl.TryValidate(release.Url, release.Version))
			_ = releaseBrowser.TryOpen(release);
	}

	private void ShowAbout()
	{
		using AboutForm aboutForm = new AboutForm(Icon, updateNotifications, releaseBrowser);
		aboutForm.ShowDialog(this);
	}

	private static Panel MakeDivider()
	{
		return new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Divider,
			Margin = Padding.Empty
		};
	}

	internal static Image LoadLogoImageForAbout()
	{
		using Stream stream = typeof(SettingsForm).Assembly.GetManifestResourceStream("EberoNest.Assets.app-icon.png") ?? throw new InvalidOperationException("No se encontró el logo oficial de EberoNest.");
		using Image image = Image.FromStream(stream);
		return new Bitmap(image);
	}

	internal PictureBox HeaderLogoForSmoke => headerLogo ?? throw new InvalidOperationException("The settings logo has not been constructed.");

	private static Button MakeButton(string text, EventHandler click, ButtonKind kind)
	{
		Color backColor = ((kind == ButtonKind.Primary) ? Accent : Surface);
		Color foreColor = ((kind == ButtonKind.Danger) ? Danger : PrimaryText);
		Color borderColor = ((kind == ButtonKind.Primary) ? Accent : Divider);
		Button button = new Button();
		button.Text = text;
		button.AutoSize = false;
		button.Height = 38;
		Button button2 = button;
		int width;
		if (kind != ButtonKind.Primary)
		{
			width = (text.StartsWith("+", StringComparison.Ordinal) ? 168 : 122);
		}
		else
		{
			width = 174;
		}
		button2.Width = width;
		button.MinimumSize = new Size((kind == ButtonKind.Primary) ? 150 : 104, 38);
		button.Margin = new Padding(0, 0, 9, 0);
		button.Padding = new Padding(12, 0, 12, 0);
		button.FlatStyle = FlatStyle.Flat;
		button.BackColor = backColor;
		button.ForeColor = foreColor;
		button.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
		button.UseVisualStyleBackColor = false;
		button.TextAlign = ContentAlignment.MiddleCenter;
		button.Cursor = Cursors.Hand;
		button.FlatAppearance.BorderColor = borderColor;
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.MouseOverBackColor = ((kind == ButtonKind.Primary) ? Color.FromArgb(39, 126, 240) : Elevated);
		button.FlatAppearance.MouseDownBackColor = ((kind == ButtonKind.Primary) ? Color.FromArgb(17, 93, 196) : Color.FromArgb(58, 63, 72));
		button.Click += click;
		return button;
	}
}

internal sealed class ToggleSwitch : Control
{
	private bool isChecked;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	internal bool Checked
	{
		get
		{
			return isChecked;
		}
		set
		{
			if (isChecked != value)
			{
				isChecked = value;
				Invalidate();
				CheckedChanged?.Invoke(this, EventArgs.Empty);
				AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
			}
		}
	}

	internal event EventHandler? CheckedChanged;

	internal ToggleSwitch()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		TabStop = true;
		AccessibleRole = AccessibleRole.CheckButton;
		BackColor = SettingsForm.Canvas;
		Cursor = Cursors.Hand;
	}

	protected override void OnClick(EventArgs e)
	{
		if (Enabled)
		{
			Checked = !Checked;
		}
		base.OnClick(e);
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Space)
		{
			Checked = !Checked;
			e.Handled = true;
			e.SuppressKeyPress = true;
		}
		base.OnKeyDown(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		int num = Math.Min(24, Height - 2);
		int num2 = Math.Min(46, Width - 2);
		int x = (Width - num2) / 2;
		int y = (Height - num) / 2;
		Rectangle rect = new Rectangle(x, y, num2, num);
		using GraphicsPath path = RoundedRectangle(rect, num / 2);
		Color color;
		if (Enabled)
		{
			color = (isChecked ? SettingsForm.Accent : SettingsForm.Elevated);
		}
		else
		{
			color = SettingsForm.Divider;
		}
		using SolidBrush brush = new SolidBrush(color);
		e.Graphics.FillPath(brush, path);
		int num3 = Math.Max(14, num - 6);
		int x2 = (isChecked ? (rect.Right - num3 - 3) : (rect.Left + 3));
		int y2 = rect.Top + (num - num3) / 2;
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(248, 249, 251));
		e.Graphics.FillEllipse(brush2, x2, y2, num3, num3);
		if (Focused)
		{
			Rectangle rectangle = Rectangle.Inflate(rect, 3, 3);
			ControlPaint.DrawFocusRectangle(e.Graphics, rectangle);
		}
	}

	protected override bool IsInputKey(Keys keyData)
	{
		if (keyData != Keys.Space)
		{
			return base.IsInputKey(keyData);
		}
		return true;
	}

	private static GraphicsPath RoundedRectangle(Rectangle rect, int radius)
	{
		int num = radius * 2;
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddArc(rect.X, rect.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(rect.Right - num, rect.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(rect.Right - num, rect.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(rect.X, rect.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}

internal sealed class AboutForm : EberoNestForm
{
	private readonly PictureBox logo = new PictureBox();

	private readonly IUpdateNotificationService updateNotifications;

	private readonly IReleaseBrowser releaseBrowser;

	private readonly Button searchUpdates = new Button();

	private readonly Label updateStatus = new Label();

	private readonly LinkLabel openUpdate = new LinkLabel();

	private readonly DarkLanguageSelector languageSelector = new DarkLanguageSelector();

	private readonly LinkLabel repositoryLink = new LinkLabel();

	private UpdateCheckResult? lastManualResult;

	private bool updatingLanguageSelector;

	internal AboutForm(Icon? ownerIcon, IUpdateNotificationService updateNotifications, IReleaseBrowser releaseBrowser)
	{
		this.updateNotifications = updateNotifications;
		this.releaseBrowser = releaseBrowser;
		Text = AppLocalization.Get("about.title");
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		ClientSize = new Size(380, 405);
		MinimumSize = Size;
		MaximumSize = Size;
		BackColor = Color.FromArgb(24, 26, 31);
		ForeColor = Color.FromArgb(236, 238, 242);
		Font = new Font("Segoe UI", 9.5f);
		if (ownerIcon != null)
		{
			Icon = (Icon)ownerIcon.Clone();
		}
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(24),
			ColumnCount = 1,
			RowCount = 11,
			BackColor = BackColor
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		logo = new PictureBox
		{
			Image = SettingsForm.LoadLogoImageForAbout(),
			SizeMode = PictureBoxSizeMode.Zoom,
			Size = new Size(68, 68),
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty
		};
		tableLayoutPanel.Controls.Add(logo, 0, 0);
		Label productName = new Label
		{
			Text = "EberoNest",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.FromArgb(236, 238, 242),
			Font = new Font("Segoe UI", 18f, FontStyle.Bold)
		};
		tableLayoutPanel.Controls.Add(productName, 0, 1);
		Label tagline = new Label
		{
			Text = AppLocalization.Get("app.tagline"),
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.TopCenter,
			ForeColor = Color.FromArgb(166, 172, 182),
			Font = new Font("Segoe UI", 9.5f)
		};
		AppLocalization.Bind(tagline, "app.tagline");
		tableLayoutPanel.Controls.Add(tagline, 0, 2);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = AppVersion.Display,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.FromArgb(166, 172, 182),
			Font = new Font("Segoe UI", 9.5f)
		}, 0, 3);
		AppLocalization.Bind(this, "about.title");
		AppLocalization.Bind(searchUpdates, "updates.search");
		searchUpdates.Text = AppLocalization.Get("updates.search");
		searchUpdates.Dock = DockStyle.Fill;
		searchUpdates.FlatStyle = FlatStyle.Flat;
		searchUpdates.BackColor = Color.FromArgb(32, 35, 41);
		searchUpdates.ForeColor = Color.FromArgb(236, 238, 242);
		searchUpdates.UseVisualStyleBackColor = false;
		searchUpdates.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
		searchUpdates.FlatAppearance.BorderColor = Color.FromArgb(43, 47, 55);
		searchUpdates.FlatAppearance.BorderSize = 1;
		searchUpdates.Click += async (_, _) => await CheckForUpdatesAsync();
		tableLayoutPanel.Controls.Add(searchUpdates, 0, 5);
		updateStatus.Dock = DockStyle.Fill;
		updateStatus.TextAlign = ContentAlignment.MiddleCenter;
		updateStatus.ForeColor = Color.FromArgb(166, 172, 182);
		updateStatus.BackColor = BackColor;
		updateStatus.Text = string.Empty;
		tableLayoutPanel.Controls.Add(updateStatus, 0, 6);
		AppLocalization.Bind(openUpdate, "updates.open");
		openUpdate.Text = AppLocalization.Get("updates.open");
		openUpdate.Dock = DockStyle.Fill;
		openUpdate.TextAlign = ContentAlignment.MiddleCenter;
		openUpdate.LinkColor = SettingsForm.Accent;
		openUpdate.ActiveLinkColor = SettingsForm.Accent;
		openUpdate.VisitedLinkColor = SettingsForm.Accent;
		openUpdate.LinkBehavior = LinkBehavior.HoverUnderline;
		openUpdate.BackColor = BackColor;
		openUpdate.Visible = false;
		openUpdate.LinkClicked += (_, _) => OpenUpdatePage();
		tableLayoutPanel.Controls.Add(openUpdate, 0, 7);
		languageSelector.Width = 190;
		languageSelector.Height = 23;
		languageSelector.Anchor = AnchorStyles.None;
		languageSelector.AccessibleName = AppLocalization.Get("about.language");
		languageSelector.SelectedIndexChanged += LanguageSelector_SelectedIndexChanged;
		tableLayoutPanel.Controls.Add(languageSelector, 0, 4);
		AppLocalization.Bind(repositoryLink, "about.repository");
		repositoryLink.Dock = DockStyle.Fill;
		repositoryLink.TextAlign = ContentAlignment.MiddleCenter;
		repositoryLink.LinkColor = SettingsForm.Accent;
		repositoryLink.ActiveLinkColor = SettingsForm.Accent;
		repositoryLink.VisitedLinkColor = SettingsForm.Accent;
		repositoryLink.LinkBehavior = LinkBehavior.HoverUnderline;
		repositoryLink.BackColor = BackColor;
		repositoryLink.LinkClicked += (_, _) => OpenOfficialRepository();
		tableLayoutPanel.Controls.Add(repositoryLink, 0, 8);
		Label license = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.FromArgb(166, 172, 182),
			BackColor = BackColor,
			Font = new Font("Segoe UI", 8.5f)
		};
		AppLocalization.Bind(license, "about.license");
		tableLayoutPanel.Controls.Add(license, 0, 9);
		Button closeButton = new Button
		{
			Text = AppLocalization.Get("button.close"),
			Dock = DockStyle.Fill,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(48, 52, 59),
			ForeColor = Color.FromArgb(236, 238, 242),
			UseVisualStyleBackColor = false,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold),
			DialogResult = DialogResult.OK
		};
		closeButton.FlatAppearance.BorderColor = Color.FromArgb(43, 47, 55);
		closeButton.FlatAppearance.BorderSize = 1;
		AppLocalization.Bind(closeButton, "button.close");
		tableLayoutPanel.Controls.Add(closeButton, 0, 10);
		Controls.Add(tableLayoutPanel);
		AcceptButton = closeButton;
		CancelButton = closeButton;
		FormClosed += (object? _, FormClosedEventArgs _) =>
		{
			AppLocalization.LanguageChanged -= Localization_LanguageChanged;
			logo.Image?.Dispose();
		};
		AppLocalization.LanguageChanged += Localization_LanguageChanged;
		RefreshLanguageSelector();
	}

	internal Button SearchUpdatesForSmoke => searchUpdates;

	internal Label UpdateStatusForSmoke => updateStatus;

	internal LinkLabel OpenUpdateForSmoke => openUpdate;

	internal DarkLanguageSelector LanguageSelectorForSmoke => languageSelector;

	internal LinkLabel RepositoryLinkForSmoke => repositoryLink;

	internal PictureBox LogoForSmoke => logo;

	internal async Task CheckForUpdatesForSmokeAsync() => await CheckForUpdatesAsync();

	internal void OpenUpdatePageForSmoke() => OpenUpdatePage();

	private async Task CheckForUpdatesAsync()
	{
		searchUpdates.Enabled = false;
		updateStatus.Text = string.Empty;
		openUpdate.Visible = false;
		try
		{
			lastManualResult = await updateNotifications.CheckManuallyAsync();
			ApplyUpdateResult();
		}
		catch (Exception)
		{
			lastManualResult = new UpdateCheckResult(UpdateCheckStatus.Failed, null);
			ApplyUpdateResult();
		}
		finally
		{
			searchUpdates.Enabled = true;
		}
	}

	private void OpenUpdatePage()
	{
		if (openUpdate.Tag is OfficialRelease release && OfficialReleaseUrl.TryValidate(release.Url, release.Version))
			_ = releaseBrowser.TryOpen(release);
	}

	private void OpenOfficialRepository() => _ = OfficialRepositoryLink.TryOpen(OfficialRepositoryLink.Url);

	private void LanguageSelector_SelectedIndexChanged(object? sender, EventArgs e)
	{
		if (updatingLanguageSelector || languageSelector.SelectedIndex < 0) return;
		AppLanguage selected = languageSelector.SelectedIndex == 0 ? AppLanguage.Spanish : AppLanguage.English;
		try { AppLocalization.SetLanguage(selected); }
		catch (Exception)
		{
			RefreshLanguageSelector();
			MessageBox.Show(this, AppLocalization.Get("language.saveFailure.body"),
				AppLocalization.Get("language.saveFailure.title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void Localization_LanguageChanged(object? sender, EventArgs e)
	{
		AppLocalization.Apply(this);
		languageSelector.AccessibleName = AppLocalization.Get("about.language");
		RefreshLanguageSelector();
		ApplyUpdateResult();
	}

	private void RefreshLanguageSelector()
	{
		updatingLanguageSelector = true;
		try
		{
			languageSelector.SetOptions(
				AppLocalization.Get(AppLanguage.Spanish, "about.language"),
				AppLocalization.Get(AppLanguage.English, "about.language"),
				AppLocalization.Current == AppLanguage.Spanish ? 0 : 1);
			languageSelector.AccessibleName = AppLocalization.Get("about.language");
		}
		finally { updatingLanguageSelector = false; }
	}

	private void ApplyUpdateResult()
	{
		openUpdate.Visible = false;
		openUpdate.Tag = null;
		updateStatus.Text = lastManualResult?.Status switch
		{
			UpdateCheckStatus.UpToDate => AppLocalization.Get("updates.current"),
			UpdateCheckStatus.UpdateAvailable when lastManualResult.LatestRelease is OfficialRelease release && OfficialReleaseUrl.TryValidate(release.Url, release.Version) =>
				AppLocalization.Get("updates.available", release.Tag),
			_ => lastManualResult is null ? string.Empty : AppLocalization.Get("updates.failure")
		};
		if (lastManualResult?.Status == UpdateCheckStatus.UpdateAvailable &&
			lastManualResult.LatestRelease is OfficialRelease available && OfficialReleaseUrl.TryValidate(available.Url, available.Version))
		{
			openUpdate.Tag = available;
			openUpdate.Visible = true;
		}
	}
}

internal static class AppVersion
{
	internal static SemanticVersion CurrentVersion
	{
		get
		{
			Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
			string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			if (!string.IsNullOrWhiteSpace(informationalVersion) &&
				SemanticVersion.TryParseTag("v" + informationalVersion.Split('+')[0], out SemanticVersion parsed))
				return parsed;

			Version? version = assembly.GetName().Version;
			if (version is not null && version.Major >= 0 && version.Minor >= 0 && version.Build >= 0)
				return new SemanticVersion(version.Major, version.Minor, version.Build);
			throw new InvalidOperationException("No se pudo leer la versión de EberoNest desde sus metadatos.");
		}
	}

	internal static string Display
	{
		get => AppVersion.CurrentVersion.ToTag();
	}
}

internal sealed class RuleEditorForm : EberoNestForm
{
	private readonly RuleCategory? original;

	private readonly IReadOnlyList<RuleCategory> existing;

	private readonly TextBox name = new TextBox();

	private readonly TextBox folder = new TextBox();

	private readonly TextBox extensions = new TextBox();

	internal RuleCategory? Result { get; private set; }

	internal RuleEditorForm(RuleCategory? category, IReadOnlyList<RuleCategory> existingRules)
	{
		original = category;
		existing = existingRules;
		Text = AppLocalization.Get(category is null ? "category.new" : "category.edit");
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		ClientSize = new Size(530, 285);
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		BackColor = SettingsForm.Canvas;
		ForeColor = Color.FromArgb(236, 238, 242);
		Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(22),
			ColumnCount = 2,
			RowCount = 5,
			BackColor = SettingsForm.Canvas,
			Margin = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
		Controls.Add(tableLayoutPanel);
		AppLocalization.Bind(this, category is null ? "category.new" : "category.edit");
		AddField(tableLayoutPanel, "category.name", name, 0);
		AddField(tableLayoutPanel, "category.destination", folder, 1);
		AddField(tableLayoutPanel, "category.extensions", extensions, 2);
		name.Text = category?.Name ?? "";
		folder.Text = category?.FolderName ?? "";
		extensions.Text = ((category == null) ? "" : string.Join(", ", category.Extensions));
		Label control = new Label
		{
			Text = AppLocalization.Get("category.extensionHelp"),
			AutoSize = true,
			ForeColor = Color.FromArgb(166, 172, 182),
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			BackColor = SettingsForm.Canvas,
			Margin = Padding.Empty
		};
		AppLocalization.Bind(control, "category.extensionHelp");
		tableLayoutPanel.Controls.Add(control, 1, 3);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.RightToLeft,
			WrapContents = false,
			Padding = new Padding(0, 5, 0, 0),
			BackColor = SettingsForm.Canvas,
			Margin = Padding.Empty
		};
		Button button = new Button
		{
			Text = AppLocalization.Get("button.save"),
			DialogResult = DialogResult.None,
			Size = new Size(104, 34),
			FlatStyle = FlatStyle.Flat,
			BackColor = SettingsForm.Accent,
			ForeColor = Color.FromArgb(255, 255, 255),
			UseVisualStyleBackColor = false,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point),
			Margin = new Padding(8, 0, 0, 0)
		};
		button.FlatAppearance.BorderColor = SettingsForm.Accent;
		button.FlatAppearance.BorderSize = 1;
		AppLocalization.Bind(button, "button.save");
		Button button2 = new Button
		{
			Text = AppLocalization.Get("button.cancel"),
			DialogResult = DialogResult.Cancel,
			Size = new Size(100, 34),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(32, 35, 41),
			ForeColor = Color.FromArgb(236, 238, 242),
			UseVisualStyleBackColor = false,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point),
			Margin = new Padding(8, 0, 0, 0)
		};
		AppLocalization.Bind(button2, "button.cancel");
		button2.FlatAppearance.BorderColor = SettingsForm.Divider;
		button2.FlatAppearance.BorderSize = 1;
		button.Click += (object? _, EventArgs _) =>
		{
			SaveRule();
		};
		flowLayoutPanel.Controls.Add(button2);
		flowLayoutPanel.Controls.Add(button);
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 4);
		tableLayoutPanel.SetColumnSpan(flowLayoutPanel, 2);
		AcceptButton = button;
		CancelButton = button2;
		AppLocalization.LanguageChanged += Localization_LanguageChanged;
		FormClosed += (_, _) => AppLocalization.LanguageChanged -= Localization_LanguageChanged;
	}

	private static void AddField(TableLayoutPanel panel, string labelKey, TextBox input, int row)
	{
		Label label = new Label
		{
			Text = AppLocalization.Get(labelKey),
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = Color.FromArgb(236, 238, 242),
			BackColor = SettingsForm.Canvas,
			Margin = Padding.Empty
		};
		AppLocalization.Bind(label, labelKey);
		panel.Controls.Add(label, 0, row);
		input.Dock = DockStyle.Fill;
		input.Margin = new Padding(0, 6, 0, 6);
		input.BorderStyle = BorderStyle.FixedSingle;
		input.BackColor = SettingsForm.Elevated;
		input.ForeColor = Color.FromArgb(236, 238, 242);
		input.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
		panel.Controls.Add(input, 1, row);
	}

	private void SaveRule()
	{
		string[] source = extensions.Text.Split(new char[6] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		RuleCategory ruleCategory = new RuleCategory
		{
			Name = name.Text.Trim(),
			FolderName = folder.Text.Trim(),
			Extensions = source.Select(RulesStore.NormalizeExtension).ToList(),
			IsBuiltIn = (original?.IsBuiltIn ?? false)
		};
		List<RuleCategory> categories = existing.Where((RuleCategory rule) => original == null || !rule.Name.Equals(original.Name, StringComparison.OrdinalIgnoreCase)).Append(ruleCategory).ToList();
		try
		{
			RulesStore.NormalizeAndValidate(categories);
		}
		catch (Exception ex) when ((ex is InvalidDataException || ex is ArgumentException) ? true : false)
		{
			MessageBox.Show(this, AppLocalization.LocalizeRuleError(ex), AppLocalization.Get("category.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		Result = ruleCategory;
		DialogResult = DialogResult.OK;
		Close();
	}

	private void Localization_LanguageChanged(object? sender, EventArgs e) => AppLocalization.Apply(this);
}

internal sealed class DarkLanguageSelector : Control
{
	private static readonly Color TextColor = Color.FromArgb(236, 238, 242);
	private static readonly Color HoverSurface = Color.FromArgb(58, 63, 72);
	private readonly ContextMenuStrip dropDown = new ContextMenuStrip();
	private readonly ToolStripMenuItem[] options = new ToolStripMenuItem[2];
	private string[] labels = [string.Empty, string.Empty];
	private int selectedIndex = -1;
	private bool hovered;

	internal DarkLanguageSelector()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint |
			ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, value: true);
		BackColor = SettingsForm.Elevated;
		ForeColor = TextColor;
		Font = new Font("Segoe UI", 9f);
		AccessibleRole = AccessibleRole.ComboBox;
		AccessibleDefaultActionDescription = "Open language options";
		TabStop = true;
		Cursor = Cursors.Hand;
		Margin = new Padding(3);
		dropDown.AutoClose = true;
		dropDown.BackColor = SettingsForm.Elevated;
		dropDown.ForeColor = TextColor;
		dropDown.Padding = new Padding(2);
		dropDown.ShowImageMargin = false;
		dropDown.ShowCheckMargin = true;
		dropDown.Renderer = new DarkMenuRenderer();
		dropDown.DropShadowEnabled = true;
		for (int index = 0; index < options.Length; index++)
		{
			int optionIndex = index;
			options[index] = new ToolStripMenuItem
			{
				AutoSize = false,
				Size = new Size(190, 28),
				Margin = Padding.Empty,
				Padding = new Padding(8, 0, 8, 0),
				BackColor = SettingsForm.Elevated,
				ForeColor = TextColor,
				Font = Font,
				CheckOnClick = false
			};
			options[index].Click += (_, _) => SelectedIndex = optionIndex;
			dropDown.Items.Add(options[index]);
		}
		dropDown.Closed += (_, _) => Invalidate();
	}

	internal event EventHandler? SelectedIndexChanged;

	[Browsable(false)]
	[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
	internal int SelectedIndex
	{
		get => selectedIndex;
		set => SetSelectedIndex(value, raiseEvent: true);
	}

	internal string? SelectedItem => selectedIndex >= 0 && selectedIndex < labels.Length ? labels[selectedIndex] : null;

	internal ToolStripDropDown DropDownForSmoke => dropDown;

	internal bool IsDropDownOpenForSmoke => dropDown.Visible;

	internal void SetHoveredForSmoke(bool value)
	{
		hovered = value;
		Invalidate();
	}

	internal Color HoverSurfaceForSmoke => HoverSurface;

	internal void SetOptions(string spanish, string english, int selected)
	{
		labels = [spanish, english];
		for (int index = 0; index < options.Length; index++)
		{
			options[index].Text = labels[index];
			options[index].AccessibleName = labels[index];
		}
		SetSelectedIndex(selected, raiseEvent: false);
	}

	internal void OpenDropDownForSmoke() => OpenDropDown();

	protected override Size DefaultSize => new Size(190, 23);

	protected override bool IsInputKey(Keys keyData) =>
		keyData is Keys.Enter or Keys.Space or Keys.Down or Keys.Up || base.IsInputKey(keyData);

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Rectangle bounds = ClientRectangle;
		if (bounds.Width <= 1 || bounds.Height <= 1) return;
		bounds.Width--;
		bounds.Height--;
		using (var surface = new SolidBrush(hovered ? HoverSurface : BackColor))
			 e.Graphics.FillRectangle(surface, bounds);
		using (var border = new Pen(Focused ? SettingsForm.Accent : SettingsForm.Divider))
			e.Graphics.DrawRectangle(border, bounds);
		string text = SelectedItem ?? string.Empty;
		Rectangle textBounds = new Rectangle(9, 0, Math.Max(0, ClientSize.Width - 34), ClientSize.Height);
		TextRenderer.DrawText(e.Graphics, text, Font, textBounds, ForeColor,
			TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
		int arrowX = ClientSize.Width - 15;
		int arrowY = ClientSize.Height / 2 - 1;
		using var arrow = new Pen(ForeColor, 1.6f);
		e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		e.Graphics.DrawLines(arrow, [new Point(arrowX - 3, arrowY - 1), new Point(arrowX, arrowY + 2), new Point(arrowX + 3, arrowY - 1)]);
	}

	protected override void OnClick(EventArgs e)
	{
		base.OnClick(e);
		OpenDropDown();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Alt && e.KeyCode == Keys.Down || e.KeyCode is Keys.Enter or Keys.Space or Keys.Down)
		{
			OpenDropDown();
			e.Handled = true;
		}
		else if (e.KeyCode == Keys.Up)
		{
			SelectedIndex = selectedIndex <= 0 ? options.Length - 1 : selectedIndex - 1;
			e.Handled = true;
		}
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);
		hovered = true;
		Invalidate();
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		hovered = false;
		Invalidate();
	}

	protected override void OnGotFocus(EventArgs e)
	{
		base.OnGotFocus(e);
		Invalidate();
	}

	protected override void OnLostFocus(EventArgs e)
	{
		base.OnLostFocus(e);
		Invalidate();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing) dropDown.Dispose();
		base.Dispose(disposing);
	}

	private void SetSelectedIndex(int value, bool raiseEvent)
	{
		if (value < 0 || value >= options.Length) throw new ArgumentOutOfRangeException(nameof(value));
		if (selectedIndex == value) return;
		selectedIndex = value;
		for (int index = 0; index < options.Length; index++) options[index].Checked = index == value;
		Invalidate();
		if (raiseEvent)
		{
			AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
			SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private void OpenDropDown()
	{
		if (!Enabled || !IsHandleCreated || dropDown.Visible) return;
		dropDown.Show(this, new Point(0, Height));
	}

	private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
	{
		internal DarkMenuRenderer() : base(new DarkMenuColorTable()) { RoundedEdges = false; }

		protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
		{
			using var fill = new SolidBrush(SettingsForm.Elevated);
			e.Graphics.FillRectangle(fill, e.AffectedBounds);
		}

		protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
		{
			using var fill = new SolidBrush(e.Item.Selected ? HoverSurface : SettingsForm.Elevated);
			e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, e.Item.Size));
		}

		protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
		{
			Rectangle bounds = e.ImageRectangle;
			using var check = new Pen(SettingsForm.Accent, 2f);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.DrawLines(check,
			[
				new Point(bounds.Left + 3, bounds.Top + bounds.Height / 2),
				new Point(bounds.Left + bounds.Width / 2 - 1, bounds.Bottom - 5),
				new Point(bounds.Right - 2, bounds.Top + 4)
			]);
		}

		protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
		{
			Rectangle bounds = e.AffectedBounds;
			bounds.Width--;
			bounds.Height--;
			using var border = new Pen(SettingsForm.Divider);
			e.Graphics.DrawRectangle(border, bounds);
		}
	}

	private sealed class DarkMenuColorTable : ProfessionalColorTable
	{
		public override Color ToolStripDropDownBackground => SettingsForm.Elevated;
		public override Color ImageMarginGradientBegin => SettingsForm.Elevated;
		public override Color ImageMarginGradientMiddle => SettingsForm.Elevated;
		public override Color ImageMarginGradientEnd => SettingsForm.Elevated;
		public override Color MenuItemSelected => HoverSurface;
		public override Color MenuItemBorder => SettingsForm.Divider;
		public override Color SeparatorDark => SettingsForm.Divider;
		public override Color SeparatorLight => SettingsForm.Elevated;
	}
}
