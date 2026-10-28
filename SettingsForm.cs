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

internal sealed class SettingsForm : Form
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

	private bool restoringStartupSwitch;

	internal SettingsForm(Organizer organizer, Func<IWin32Window?, Task> organize, IStartupManager startupManager)
	{
		this.organizer = organizer;
		this.organize = organize;
		this.startupManager = startupManager;
		Text = "EberoNest";
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
			headerLogo?.Image?.Dispose();
		};
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
		flowLayoutPanel.Controls.Add(new Label
		{
			Text = "Your downloads, automatically sorted.",
			AutoSize = true,
			ForeColor = SecondaryText,
			Font = new Font("Segoe UI", 10.5f),
			Margin = new Padding(1, 1, 0, 0)
		});
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 1, 0);
		return tableLayoutPanel;
	}

	private Control BuildSectionHeading()
	{
		return new TableLayoutPanel
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
			Controls = 
			{
				{
					(Control)new Label
					{
						Text = "Reglas de organización",
						Dock = DockStyle.Fill,
						Font = new Font("Segoe UI", 18f, FontStyle.Bold),
						ForeColor = PrimaryText,
						TextAlign = ContentAlignment.MiddleLeft,
						Margin = Padding.Empty
					},
					0,
					0
				},
				{
					(Control)new Label
					{
						Text = "Define cómo se organizan tus descargas automáticamente.",
						Dock = DockStyle.Fill,
						ForeColor = SecondaryText,
						Font = Font,
						TextAlign = ContentAlignment.MiddleLeft,
						Margin = Padding.Empty
					},
					0,
					1
				}
			}
		};
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
		flowLayoutPanel.Controls.Add(MakeButton("+  Nueva categoría", (object? _, EventArgs _) =>
		{
			AddRule();
		}, ButtonKind.Primary));
		flowLayoutPanel.Controls.Add(MakeButton("Editar", (object? _, EventArgs _) =>
		{
			EditSelected();
		}, ButtonKind.Neutral));
		flowLayoutPanel.Controls.Add(MakeButton("Eliminar", (object? _, EventArgs _) =>
		{
			DeleteSelected();
		}, ButtonKind.Danger));
		Button button = MakeButton("▶  Organizar ahora", async (object? _, EventArgs _) =>
		{
			await organize(this);
		}, ButtonKind.Primary);
		button.Dock = DockStyle.None;
		button.Size = new Size(174, 38);
		button.Anchor = AnchorStyles.Right;
		button.Margin = Padding.Empty;
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
		startupSwitch.AccessibleName = "Iniciar EberoNest con Windows";
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel
		{
			AutoSize = true,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			BackColor = Canvas,
			Margin = Padding.Empty
		};
		flowLayoutPanel2.Controls.Add(new Label
		{
			Text = "Iniciar EberoNest con Windows",
			AutoSize = true,
			ForeColor = PrimaryText,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold),
			Margin = Padding.Empty
		});
		flowLayoutPanel2.Controls.Add(new Label
		{
			Text = "Ejecutar silenciosamente al iniciar sesión.",
			AutoSize = true,
			ForeColor = SecondaryText,
			Font = new Font("Segoe UI", 8.5f),
			Margin = new Padding(0, 3, 0, 0)
		});
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
		tableLayoutPanel2.Controls.Add(new Label
		{
			Text = "Organizador activo",
			Dock = DockStyle.Fill,
			ForeColor = PrimaryText,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = Padding.Empty
		}, 1, 0);
		status.Text = "Monitoreando la carpeta de Descargas.";
		status.Dock = DockStyle.Fill;
		status.ForeColor = SecondaryText;
		status.Font = new Font("Segoe UI", 8.5f);
		status.TextAlign = ContentAlignment.TopLeft;
		tableLayoutPanel2.Controls.Add(status, 1, 1);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 1, 0);
		Button button = MakeButton("Acerca de · " + AppVersion.Display, (object? _, EventArgs _) =>
		{
			ShowAbout();
		}, ButtonKind.Neutral);
		button.Dock = DockStyle.None;
		button.Size = new Size(172, 38);
		button.Anchor = AnchorStyles.Right;
		button.MinimumSize = new Size(172, 38);
		button.Margin = Padding.Empty;
		tableLayoutPanel.Controls.Add(button, 2, 0);
		return tableLayoutPanel;
	}

		internal RulesListControl RulesListForSmoke => rulesList;

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
			MessageBox.Show(this, "Las categorías incluidas no se pueden eliminar.", "EberoNest", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		else if (MessageBox.Show(this, "¿Eliminar la categoría «" + selected.Name + "»? Los archivos que ya estén en esa carpeta no se moverán.", "Confirmar eliminación", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
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
			status.Text = "Reglas guardadas. Cambios aplicados.";
		}
		catch (Exception ex) when ((ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) ? true : false)
		{
			MessageBox.Show(this, ex.Message, "No se pudo guardar la regla", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
			MessageBox.Show(this, "No se pudo cambiar el inicio automático de EberoNest.\n" + ex.Message, "Error de inicio automático", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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

	private void ShowAbout()
	{
		using AboutForm aboutForm = new AboutForm(Icon);
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
		using Stream stream = typeof(SettingsForm).Assembly.GetManifestResourceStream("EberoNest.Assets.app-icon.ico") ?? throw new InvalidOperationException("No se encontró el icono oficial de EberoNest.");
		using Icon icon = new Icon(stream);
		return icon.ToBitmap();
	}

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

internal sealed class AboutForm : Form
{
	internal AboutForm(Icon? ownerIcon)
	{
		Text = "Acerca de EberoNest";
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96f, 96f);
		ClientSize = new Size(380, 330);
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
			RowCount = 6,
			BackColor = BackColor
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 80f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
		PictureBox logo = new PictureBox
		{
			Image = SettingsForm.LoadLogoImageForAbout(),
			SizeMode = PictureBoxSizeMode.Zoom,
			Size = new Size(72, 72),
			Anchor = AnchorStyles.None,
			Margin = Padding.Empty
		};
		tableLayoutPanel.Controls.Add(logo, 0, 0);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "EberoNest",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.FromArgb(236, 238, 242),
			Font = new Font("Segoe UI", 18f, FontStyle.Bold)
		}, 0, 1);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Your downloads, automatically sorted.",
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.TopCenter,
			ForeColor = Color.FromArgb(166, 172, 182),
			Font = new Font("Segoe UI", 9.5f)
		}, 0, 2);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = AppVersion.Display,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.FromArgb(166, 172, 182),
			Font = new Font("Segoe UI", 9.5f)
		}, 0, 4);
		Button button = new Button
		{
			Text = "Cerrar",
			Dock = DockStyle.Fill,
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(48, 52, 59),
			ForeColor = Color.FromArgb(236, 238, 242),
			UseVisualStyleBackColor = false,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold),
			DialogResult = DialogResult.OK
		};
		button.FlatAppearance.BorderColor = Color.FromArgb(43, 47, 55);
		button.FlatAppearance.BorderSize = 1;
		tableLayoutPanel.Controls.Add(button, 0, 5);
		Controls.Add(tableLayoutPanel);
		AcceptButton = button;
		FormClosed += (object? _, FormClosedEventArgs _) =>
		{
			logo.Image?.Dispose();
		};
	}
}

internal static class AppVersion
{
	internal static string Display
	{
		get
		{
			Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
			string? text = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			if (!string.IsNullOrWhiteSpace(text))
			{
				return "v" + text.Split('+')[0];
			}
			Version? version = assembly.GetName().Version;
			if (version is not null)
			{
				return "v" + version.ToString(3);
			}
			return "v—";
		}
	}
}

internal sealed class RuleEditorForm : Form
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
		Text = ((category == null) ? "Nueva categoría" : "Editar categoría");
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
		AddField(tableLayoutPanel, "Nombre", name, 0);
		AddField(tableLayoutPanel, "Carpeta de destino", folder, 1);
		AddField(tableLayoutPanel, "Extensiones", extensions, 2);
		name.Text = category?.Name ?? "";
		folder.Text = category?.FolderName ?? "";
		extensions.Text = ((category == null) ? "" : string.Join(", ", category.Extensions));
		Label control = new Label
		{
			Text = "Separa extensiones con coma o espacio, por ejemplo: stl, 3mf, obj",
			AutoSize = true,
			ForeColor = Color.FromArgb(166, 172, 182),
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			BackColor = SettingsForm.Canvas,
			Margin = Padding.Empty
		};
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
			Text = "Guardar",
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
		Button button2 = new Button
		{
			Text = "Cancelar",
			DialogResult = DialogResult.Cancel,
			Size = new Size(100, 34),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.FromArgb(32, 35, 41),
			ForeColor = Color.FromArgb(236, 238, 242),
			UseVisualStyleBackColor = false,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point),
			Margin = new Padding(8, 0, 0, 0)
		};
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
	}

	private static void AddField(TableLayoutPanel panel, string label, TextBox input, int row)
	{
		panel.Controls.Add(new Label
		{
			Text = label,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = Color.FromArgb(236, 238, 242),
			BackColor = SettingsForm.Canvas,
			Margin = Padding.Empty
		}, 0, row);
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
			MessageBox.Show(this, ex.Message, "Revisa los datos", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		Result = ruleCategory;
		DialogResult = DialogResult.OK;
		Close();
	}
}
