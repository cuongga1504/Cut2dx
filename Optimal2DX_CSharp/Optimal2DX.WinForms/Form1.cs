#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Optimal2DX;
using Optimal2DX.Models;

namespace Optimal2DX.WinForms;

public partial class Form1 : Form
{
    private readonly Optimization2DX _opt = new Optimization2DX();
    private int _currentDisplayIndex = 0; // Current viewed Pattern or Sheet index
    private bool _isGroupedMode = true;   // Default: group identical cutting patterns
    private List<GroupedSheetLayout> _groupedLayouts = new();

    // Track input metadata per demand piece
    private class DemandPieceMeta
    {
        public int Length { get; set; }
        public int Width { get; set; }
        public GrainDirection Grain { get; set; }
        public int ExternalID { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    // Track input metadata per repository sheet
    private class RepoPieceMeta
    {
        public int Length { get; set; }
        public int Width { get; set; }
        public SheetGrainDirection Grain { get; set; }
        public string Label { get; set; } = string.Empty;
        public int ExternalID { get; set; }
    }

    private readonly List<DemandPieceMeta> _demandMeta = new();
    private readonly List<RepoPieceMeta> _repoMeta = new();

    public enum WasteHatchStyle
    {
        DiagonalCross,     // Gạch chéo đan lưới (X)
        BackwardDiagonal,  // Gạch xiên chéo (/)
        ForwardDiagonal,   // Gạch xiên chéo (\)
        CornerX,           // Đường chéo chữ X nối 4 góc
        Solid              // Màu cam trơn
    }

    private WasteHatchStyle _wasteHatchStyle = WasteHatchStyle.DiagonalCross;
    private ToolStripComboBox _cboWasteHatch;

    // Controls
    private SplitContainer _mainSplit;
    private TabControl _tabInput;
    private DataGridView _dgvDemand;
    private DataGridView _dgvRepo;
    private ComboBox _cboMethod;
    private NumericUpDown _numBladeWidth;
    private NumericUpDown _numOptLevel;
    private NumericUpDown _numSeed;
    private NumericUpDown _numMinUseful;
    private NumericUpDown _numMaxCutLen;
    private Button _btnRun;
    private ProgressBar _progressBar;
    private CheckBox _chkMinimizePatterns;

    private PictureBox _picCanvas;
    private Button _btnPrevSheet;
    private Button _btnNextSheet;
    private Label _lblSheetInfo;
    private Label _lblStats;
    private CheckBox _chkGroupIdentical;

    private TabControl _tabResults;
    private DataGridView _dgvPatterns;
    private DataGridView _dgvUtilized;
    private DataGridView _dgvCuts;
    private DataGridView _dgvWaste;

    public Form1()
    {
        InitializeComponent();
        BuildUI();
        LoadImageSampleData(false);
    }

    private void BuildUI()
    {
        this.Font = new Font("Segoe UI", 9.5f);
        this.BackColor = Color.FromArgb(245, 247, 250);

        // Top ToolStrip
        var toolStrip = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.White,
            Padding = new Padding(10, 6, 10, 6),
            RenderMode = ToolStripRenderMode.System
        };

        var btnImageSample = new ToolStripButton("📸  Mẫu theo ảnh (1.050 chi tiết / 163 tấm)", null, (s, e) => LoadImageSampleData(true))
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 204)
        };
        var btnLoadSample = new ToolStripButton("  Ví dụ chuẩn (VB 2005)", null, (s, e) => LoadDefaultExampleData())
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        var btnGrainSample = new ToolStripButton("🪵  Ví dụ Thớ gỗ & Số lượng", null, (s, e) => LoadGrainSampleData())
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(140, 70, 20)
        };
        var btnBatchSample = new ToolStripButton("🏭  Ví dụ Cắt hàng loạt (Gom sơ đồ)", null, (s, e) => LoadBatchSampleData())
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 130, 90)
        };
        var btnRunTool = new ToolStripButton("▶  Chạy tối ưu hóa", null, (s, e) => RunOptimization())
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 215)
        };
        var btnExportHtml = new ToolStripButton("🌐  Xuất báo cáo HTML", null, (s, e) => ExportHtmlReport())
        {
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 124, 65)
        };
        var btnExport = new ToolStripButton("💾  Xuất file TXT", null, (s, e) => ExportResults())
        {
            Font = new Font("Segoe UI", 9.5f)
        };
        var btnHelpGrain = new ToolStripButton("📖  Tài liệu Thớ gỗ", null, (s, e) => ShowGrainHelp())
        {
            Font = new Font("Segoe UI", 9.5f)
        };
        var btnClear = new ToolStripButton("✕  Xóa sạch", null, (s, e) => ClearAllData())
        {
            Font = new Font("Segoe UI", 9.5f)
        };

        var lblWasteHatch = new ToolStripLabel("Phế liệu:")
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(170, 90, 20)
        };
        _cboWasteHatch = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            ToolTipText = "Kiểu hiển thị gạch chéo cho phần phế liệu (Waste Pieces)",
            Width = 195,
            Font = new Font("Segoe UI", 9f)
        };
        _cboWasteHatch.Items.AddRange(new object[] {
            "☒ Gạch chéo đan lưới",
            "▨ Gạch xiên chéo (/)",
            "▧ Gạch xiên chéo (\\)",
            "✕ Đường chéo chữ X",
            "■ Màu cam trơn"
        });
        _cboWasteHatch.SelectedIndex = 0; // Mặc định: Gạch chéo đan lưới
        _cboWasteHatch.SelectedIndexChanged += (s, e) =>
        {
            _wasteHatchStyle = (WasteHatchStyle)_cboWasteHatch.SelectedIndex;
            _picCanvas.Invalidate();
        };

        toolStrip.Items.AddRange(new ToolStripItem[] {
            btnImageSample,
            btnLoadSample,
            btnGrainSample,
            btnBatchSample,
            new ToolStripSeparator(),
            btnRunTool,
            new ToolStripSeparator(),
            btnExportHtml,
            btnExport,
            btnHelpGrain,
            new ToolStripSeparator(),
            lblWasteHatch,
            _cboWasteHatch,
            new ToolStripSeparator(),
            btnClear
        });
        this.Controls.Add(toolStrip);

        // Main SplitContainer
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 610,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(220, 224, 230)
        };
        this.Controls.Add(_mainSplit);
        _mainSplit.BringToFront();

        // -------------------------------------------------------------
        // LEFT PANEL: Inputs & Parameters
        // -------------------------------------------------------------
        var leftPanel = _mainSplit.Panel1;
        leftPanel.BackColor = Color.White;
        leftPanel.Padding = new Padding(10);

        _tabInput = new TabControl { Dock = DockStyle.Fill };

        // Tab 1: Demand pieces
        var pageDemand = new TabPage(" Chi tiết cần cắt (Demand) ");
        pageDemand.Padding = new Padding(8);

        // Info Banner for Quantity, Grain Direction & Labels
        var pnlDemandBanner = CreateInfoBanner(
            "🏷️ Nhập theo Quy cách & Số lượng (SL): Nhập mỗi quy cách một dòng kèm Số lượng (SL). " +
            "Hệ thống tự động gom các sơ đồ cắt giống hệt nhau (Pattern Grouping) để xưởng cưa hàng loạt nhanh chóng.",
            () => ShowGrainHelp()
        );

        _dgvDemand = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AllowUserToResizeRows = false
        };

        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Name = "Index", ReadOnly = true, FillWeight = 10 });
        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chiều dài", Name = "Length", FillWeight = 26 });
        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chiều rộng", Name = "Width", FillWeight = 26 });
        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Số lượng", Name = "Quantity", FillWeight = 22 });
        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Vật liệu", Name = "Material", FillWeight = 22 });

        var colDemandGrain = new DataGridViewComboBoxColumn
        {
            HeaderText = "Thớ gỗ",
            Name = "Grain",
            FillWeight = 40,
            FlatStyle = FlatStyle.Flat
        };
        colDemandGrain.Items.AddRange(new object[] {
            "≡ Dọc (Theo Dài)",
            "||| Ngang (Theo Rộng)",
            "🔄 Tự do (Không thớ)",
            "🔒 Cố định (Không xoay)"
        });
        _dgvDemand.Columns.Add(colDemandGrain);

        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nhãn", Name = "Label", FillWeight = 32 });
        _dgvDemand.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Xoay?", Name = "CanRotate", FillWeight = 16 });
        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ext ID", Name = "ExternalID", FillWeight = 16 });
        _dgvDemand.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ưu tiên", Name = "Priority", FillWeight = 16 });

        // Auto sync Grain and CanRotate
        _dgvDemand.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (_dgvDemand.IsCurrentCellDirty)
                _dgvDemand.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _dgvDemand.CellValueChanged += DgvDemand_CellValueChanged;

        var pnlDemandBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 4, 0, 0) };
        var btnAddDemand = new Button { Text = "+ Thêm chi tiết", AutoSize = true, BackColor = Color.FromArgb(240, 244, 250), FlatStyle = FlatStyle.Flat };
        btnAddDemand.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 225);
        btnAddDemand.Click += (s, e) =>
        {
            int idx = _dgvDemand.Rows.Count + 1;
            AddDemandRow(idx, 50, 50, 1, "≡ Dọc (Theo Dài)", $"Chi tiết #{idx}");
        };

        var btnDelDemand = new Button { Text = "- Xóa dòng chọn", AutoSize = true, BackColor = Color.FromArgb(250, 240, 240), FlatStyle = FlatStyle.Flat };
        btnDelDemand.FlatAppearance.BorderColor = Color.FromArgb(235, 200, 200);
        btnDelDemand.Click += (s, e) =>
        {
            if (_dgvDemand.CurrentRow != null && !_dgvDemand.CurrentRow.IsNewRow)
                _dgvDemand.Rows.Remove(_dgvDemand.CurrentRow);
        };
        pnlDemandBtns.Controls.AddRange(new Control[] { btnAddDemand, btnDelDemand });

        pageDemand.Controls.Add(_dgvDemand);
        pageDemand.Controls.Add(pnlDemandBtns);
        pageDemand.Controls.Add(pnlDemandBanner);

        // Tab 2: Repository pieces (Stock)
        var pageRepo = new TabPage(" Các tấm vật liệu được sử dụng (Stock) ");
        pageRepo.Padding = new Padding(8);

        var pnlRepoBanner = CreateInfoBanner(
            "🪵 Các tấm vật liệu được sử dụng: Khai báo kích thước, số lượng tấm tồn kho và phần cắt bỏ viền (Trim: Trên, Trái, Dưới, Phải). " +
            "Thuật toán chỉ dùng đúng số lượng tấm cần thiết và tự động gom nhóm các tấm có sơ đồ cắt giống nhau.",
            () => ShowGrainHelp()
        );

        _dgvRepo = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AllowUserToResizeRows = false
        };
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "#", Name = "Index", ReadOnly = true, FillWeight = 10 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chiều dài", Name = "Length", FillWeight = 24 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Chiều rộng", Name = "Width", FillWeight = 24 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Số lượng", Name = "Quantity", FillWeight = 20 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Vật liệu", Name = "Material", FillWeight = 20 });

        var colRepoGrain = new DataGridViewComboBoxColumn
        {
            HeaderText = "Thớ gỗ",
            Name = "SheetGrain",
            FillWeight = 38,
            FlatStyle = FlatStyle.Flat
        };
        colRepoGrain.Items.AddRange(new object[] {
            "≡ Dọc (Theo Dài)",
            "||| Ngang (Theo Rộng)",
            "🔄 Không thớ"
        });
        _dgvRepo.Columns.Add(colRepoGrain);

        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nhãn", Name = "Label", FillWeight = 28 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Giá", Name = "Price", FillWeight = 15 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trên", Name = "TrimTop", FillWeight = 15 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Trái", Name = "TrimLeft", FillWeight = 15 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Dưới", Name = "TrimBottom", FillWeight = 15 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Phải", Name = "TrimRight", FillWeight = 15 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mức độ ưu tiên", Name = "Priority", FillWeight = 20 });
        _dgvRepo.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ext ID", Name = "ExternalID", FillWeight = 16 });

        var pnlRepoBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 4, 0, 0) };
        var btnAddRepo = new Button { Text = "+ Thêm tấm kho", AutoSize = true, BackColor = Color.FromArgb(240, 244, 250), FlatStyle = FlatStyle.Flat };
        btnAddRepo.FlatAppearance.BorderColor = Color.FromArgb(200, 210, 225);
        btnAddRepo.Click += (s, e) =>
        {
            int idx = _dgvRepo.Rows.Count + 1;
            AddRepoRow(idx, 2440, 1220, 1, "≡ Dọc (Theo Dài)", $"Tấm #{idx}");
        };
        var btnDelRepo = new Button { Text = "- Xóa tấm", AutoSize = true, BackColor = Color.FromArgb(250, 240, 240), FlatStyle = FlatStyle.Flat };
        btnDelRepo.FlatAppearance.BorderColor = Color.FromArgb(235, 200, 200);
        btnDelRepo.Click += (s, e) =>
        {
            if (_dgvRepo.CurrentRow != null && !_dgvRepo.CurrentRow.IsNewRow)
                _dgvRepo.Rows.Remove(_dgvRepo.CurrentRow);
        };
        pnlRepoBtns.Controls.AddRange(new Control[] { btnAddRepo, btnDelRepo });

        pageRepo.Controls.Add(_dgvRepo);
        pageRepo.Controls.Add(pnlRepoBtns);
        pageRepo.Controls.Add(pnlRepoBanner);

        // Tab 3: Settings
        var pageSettings = new TabPage(" Tham số tối ưu ");
        pageSettings.Padding = new Padding(12);
        var tableSettings = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 7,
            AutoSize = true,
            Padding = new Padding(5)
        };
        tableSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        tableSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        tableSettings.Controls.Add(new Label { Text = "Thuật toán cắt:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _cboMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _cboMethod.Items.AddRange(new object[] {
            "1. Guillotine (Cắt thẳng mép tới mép)",
            "2. Non-Guillotine / Nested (Laser/Plasma)",
            "3. Multi-Stages (Cắt phân tầng)"
        });
        _cboMethod.SelectedIndex = 0;
        tableSettings.Controls.Add(_cboMethod, 1, 0);

        tableSettings.Controls.Add(new Label { Text = "Bề dày mạch cưa (BladeWidth):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _numBladeWidth = new NumericUpDown { Minimum = 0, Maximum = 100, Value = 0, Dock = DockStyle.Fill };
        tableSettings.Controls.Add(_numBladeWidth, 1, 1);

        tableSettings.Controls.Add(new Label { Text = "Cấp độ tối ưu (OptimizationLevel 1-100):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _numOptLevel = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 50, Dock = DockStyle.Fill };
        tableSettings.Controls.Add(_numOptLevel, 1, 2);

        tableSettings.Controls.Add(new Label { Text = "Seed ngẫu nhiên (RandomSeed):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _numSeed = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = 1, Dock = DockStyle.Fill };
        tableSettings.Controls.Add(_numSeed, 1, 3);

        tableSettings.Controls.Add(new Label { Text = "Kích thước phế liệu hữu ích (MinSize):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _numMinUseful = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = 20, Dock = DockStyle.Fill };
        tableSettings.Controls.Add(_numMinUseful, 1, 4);

        tableSettings.Controls.Add(new Label { Text = "Chiều dài nhát cắt tối đa (MaxCutLen):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
        _numMaxCutLen = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = 0, Dock = DockStyle.Fill };
        tableSettings.Controls.Add(_numMaxCutLen, 1, 5);

        tableSettings.Controls.Add(new Label { Text = "Mục tiêu tối ưu:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 6);
        _chkMinimizePatterns = new CheckBox
        {
            Text = "Gom giảm số lượng sơ đồ (Batch Mode)",
            Checked = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 204)
        };
        tableSettings.Controls.Add(_chkMinimizePatterns, 1, 6);

        _btnRun = new Button
        {
            Text = "▶  BẮT ĐẦU TỐI ƯU HÓA",
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(16, 124, 65),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 15, 0, 10)
        };
        _btnRun.FlatAppearance.BorderSize = 0;
        _btnRun.Click += (s, e) => RunOptimization();

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 12,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 0
        };

        pageSettings.Controls.Add(_progressBar);
        pageSettings.Controls.Add(_btnRun);
        pageSettings.Controls.Add(tableSettings);

        _tabInput.TabPages.AddRange(new TabPage[] { pageDemand, pageRepo, pageSettings });
        leftPanel.Controls.Add(_tabInput);

        // -------------------------------------------------------------
        // RIGHT PANEL: Canvas & Results
        // -------------------------------------------------------------
        var rightPanel = _mainSplit.Panel2;
        rightPanel.BackColor = Color.White;
        rightPanel.Padding = new Padding(10);

        // Navigation & Summary Bar
        var topBar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(240, 244, 248), Padding = new Padding(8) };

        _btnPrevSheet = new Button { Text = "◀ Sơ đồ trước", Width = 110, Height = 34, Dock = DockStyle.Left, Enabled = false };
        _btnPrevSheet.Click += (s, e) =>
        {
            if (_currentDisplayIndex > 0)
            {
                _currentDisplayIndex--;
                UpdateUIResults();
            }
        };

        _lblSheetInfo = new Label
        {
            Text = "Sơ đồ: 0 / 0",
            AutoSize = false,
            Width = 145,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Left,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _btnNextSheet = new Button { Text = "Sơ đồ sau ▶", Width = 110, Height = 34, Dock = DockStyle.Left, Enabled = false };
        _btnNextSheet.Click += (s, e) =>
        {
            int maxIdx = _isGroupedMode ? (_groupedLayouts.Count - 1) : (_opt.TotalNumberOfUtilizedRepositoryPieces - 1);
            if (_currentDisplayIndex < maxIdx)
            {
                _currentDisplayIndex++;
                UpdateUIResults();
            }
        };

        // CheckBox to toggle grouping
        _chkGroupIdentical = new CheckBox
        {
            Text = "Gom sơ đồ giống nhau",
            Checked = true,
            AutoSize = true,
            Dock = DockStyle.Left,
            Padding = new Padding(14, 8, 8, 0),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 100, 180),
            Cursor = Cursors.Hand
        };
        _chkGroupIdentical.CheckedChanged += (s, e) =>
        {
            _isGroupedMode = _chkGroupIdentical.Checked;
            _currentDisplayIndex = 0;
            UpdateUIResults();
        };

        _lblStats = new Label
        {
            Text = "Chưa có kết quả. Bấm 'Bắt đầu tối ưu hóa' để chạy.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 40, 60)
        };

        topBar.Controls.Add(_lblStats);
        topBar.Controls.Add(_chkGroupIdentical);
        topBar.Controls.Add(_btnNextSheet);
        topBar.Controls.Add(_lblSheetInfo);
        topBar.Controls.Add(_btnPrevSheet);
        rightPanel.Controls.Add(topBar);

        // Center: Canvas PictureBox
        _picCanvas = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _picCanvas.Paint += PicCanvas_Paint;
        _picCanvas.Resize += (s, e) => _picCanvas.Invalidate();
        rightPanel.Controls.Add(_picCanvas);
        _picCanvas.BringToFront();

        // Bottom TabControl for details
        _tabResults = new TabControl { Dock = DockStyle.Bottom, Height = 220 };

        // Tab 0: Summary Patterns
        var pagePatterns = new TabPage(" 📊 Tổng hợp sơ đồ (Patterns) ");
        _dgvPatterns = CreateResultGrid();
        _dgvPatterns.Columns.Add("Pattern", "Mẫu sơ đồ");
        _dgvPatterns.Columns.Add("SheetLabel", "Tên tấm kho");
        _dgvPatterns.Columns.Add("SheetSize", "Kích thước tấm");
        _dgvPatterns.Columns.Add("Count", "Số lượng cắt (SL)");
        _dgvPatterns.Columns.Add("PiecesPerSheet", "Chi tiết / Tấm");
        _dgvPatterns.Columns.Add("TotalPieces", "Tổng chi tiết");
        _dgvPatterns.Columns.Add("Covered", "Độ phủ (%)");
        _dgvPatterns.Columns.Add("CutLen", "Mạch cắt (mm)");
        _dgvPatterns.Columns.Add("WasteArea", "Phế liệu / Tấm");

        // Double-click row in summary jumps to that pattern
        _dgvPatterns.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _groupedLayouts.Count)
            {
                _chkGroupIdentical.Checked = true;
                _isGroupedMode = true;
                _currentDisplayIndex = e.RowIndex;
                UpdateUIResults();
            }
        };
        pagePatterns.Controls.Add(_dgvPatterns);

        // Tab 1: Utilized Pieces
        var pageUtilized = new TabPage(" Chi tiết trên sơ đồ (Utilized Pieces) ");
        _dgvUtilized = CreateResultGrid();
        _dgvUtilized.Columns.Add("Idx", "#");
        _dgvUtilized.Columns.Add("Label", "Tên / Nhãn chi tiết");
        _dgvUtilized.Columns.Add("Pos", "Tọa độ (X1, Y1) - (X2, Y2)");
        _dgvUtilized.Columns.Add("Size", "Kích thước (WxH)");
        _dgvUtilized.Columns.Add("QtyPerSheet", "SL / Tấm");
        _dgvUtilized.Columns.Add("TotalQty", "Tổng SL (các tấm)");
        _dgvUtilized.Columns.Add("Grain", "Thớ gỗ (Grain Direction)");
        _dgvUtilized.Columns.Add("Rot", "Xoay?");
        _dgvUtilized.Columns.Add("ID", "DemID");
        pageUtilized.Controls.Add(_dgvUtilized);

        // Tab 2: Cuts
        var pageCuts = new TabPage(" Nhát cắt Guillotine (Cuts) ");
        _dgvCuts = CreateResultGrid();
        _dgvCuts.Columns.Add("Idx", "Nhát #");
        _dgvCuts.Columns.Add("Coords", "Đoạn cắt (tx, ty) -> (bx, by)");
        _dgvCuts.Columns.Add("Dir", "Hướng");
        _dgvCuts.Columns.Add("Thick", "Độ dày");
        _dgvCuts.Columns.Add("Level", "Tầng (Stage)");
        pageCuts.Controls.Add(_dgvCuts);

        // Tab 3: Wastes
        var pageWaste = new TabPage(" Phế liệu (Wastes) ");
        _dgvWaste = CreateResultGrid();
        _dgvWaste.Columns.Add("Idx", "Mảnh #");
        _dgvWaste.Columns.Add("Pos", "Tọa độ (X1, Y1) - (X2, Y2)");
        _dgvWaste.Columns.Add("Size", "Kích thước (WxH)");
        _dgvWaste.Columns.Add("Area1", "Diện tích 1 tấm");
        _dgvWaste.Columns.Add("TotalArea", "Tổng DT (các tấm)");
        _dgvWaste.Columns.Add("Class", "Phân loại");
        _dgvWaste.Columns.Add("GrainAvail", "Thớ phôi tái sử dụng");
        pageWaste.Controls.Add(_dgvWaste);

        _tabResults.TabPages.AddRange(new TabPage[] { pagePatterns, pageUtilized, pageCuts, pageWaste });
        rightPanel.Controls.Add(_tabResults);
    }

    private Panel CreateInfoBanner(string text, Action onHelpClick)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(254, 250, 240),
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 6)
        };

        var lbl = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(90, 60, 20)
        };

        var btnHelp = new Button
        {
            Text = "Chi tiết ↗",
            Dock = DockStyle.Right,
            Width = 72,
            Height = 28,
            BackColor = Color.FromArgb(245, 235, 220),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(130, 60, 10),
            Cursor = Cursors.Hand
        };
        btnHelp.FlatAppearance.BorderColor = Color.FromArgb(220, 190, 150);
        btnHelp.Click += (s, e) => onHelpClick?.Invoke();

        panel.Controls.Add(lbl);
        panel.Controls.Add(btnHelp);
        return panel;
    }

    private void DgvDemand_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _dgvDemand.Rows.Count) return;

        var row = _dgvDemand.Rows[e.RowIndex];
        string colName = _dgvDemand.Columns[e.ColumnIndex].Name;

        if (colName == "Grain")
        {
            string grainStr = row.Cells["Grain"].Value?.ToString() ?? "";
            bool canRot = grainStr.Contains("Tự do");
            row.Cells["CanRotate"].Value = canRot;
        }
        else if (colName == "CanRotate")
        {
            bool canRot = Convert.ToBoolean(row.Cells["CanRotate"].Value ?? false);
            string curGrain = row.Cells["Grain"].Value?.ToString() ?? "";
            if (canRot && !curGrain.Contains("Tự do"))
            {
                row.Cells["Grain"].Value = "🔄 Tự do (Không thớ)";
            }
            else if (!canRot && curGrain.Contains("Tự do"))
            {
                row.Cells["Grain"].Value = "≡ Dọc (Theo Dài)";
            }
        }
    }

    private DataGridView CreateResultGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            ReadOnly = true,
            AllowUserToAddRows = false
        };
    }

    private void AddDemandRow(int index, int length, int width, int quantity, string grain = "≡ Dọc (Theo Dài)", string label = "", string material = "", bool? canRotate = null, int extId = 0, int priority = 0)
    {
        bool rot = canRotate ?? grain.Contains("Tự do");
        _dgvDemand.Rows.Add(index, length, width, quantity, material, grain, label, rot, extId, priority);
    }

    private void AddRepoRow(int index, int length, int width, int quantity, string grain = "≡ Dọc (Theo Dài)", string label = "", string material = "", int price = 0, int trimTop = 0, int trimLeft = 0, int trimBottom = 0, int trimRight = 0, int priority = 0, int extId = 0)
    {
        _dgvRepo.Rows.Add(index, length, width, quantity, material, grain, label, price, trimTop, trimLeft, trimBottom, trimRight, priority, extId);
    }

    /// <summary>
    /// Cài đặt bộ dữ liệu mẫu theo hình ảnh:
    /// - 8 quy cách chi tiết (tổng cộng 1.050 chi tiết) với thớ gỗ ≡
    /// - 1 quy cách tấm vật liệu được sử dụng: 2440 x 1220 mm (163 tấm), Thớ gỗ ≡, Cắt bỏ: Trên 2, Trái 2, Dưới 0, Phải 0
    /// </summary>
    private void LoadImageSampleData(bool showMessage = false)
    {
        _dgvDemand.Rows.Clear();
        _dgvRepo.Rows.Clear();

        // 8 dòng quy cách chi tiết cần cắt theo đúng hình ảnh (Tổng 1.050 chi tiết):
        // #: 1..8 | Chiều dài | Chiều rộng | Số lượng | Vật liệu | Thớ gỗ | Nhãn
        AddDemandRow(1, 1568,  953,  45, "≡ Dọc (Theo Dài)");
        AddDemandRow(2,  754, 1008,  45, "≡ Dọc (Theo Dài)");
        AddDemandRow(3, 1284,  959,  36, "≡ Dọc (Theo Dài)");
        AddDemandRow(4,  618,  642,  96, "≡ Dọc (Theo Dài)");
        AddDemandRow(5,  482,  340, 288, "≡ Dọc (Theo Dài)");
        AddDemandRow(6,  779,  340, 180, "≡ Dọc (Theo Dài)");
        AddDemandRow(7,  663,  340, 270, "≡ Dọc (Theo Dài)");
        AddDemandRow(8,  650,  340,  90, "≡ Dọc (Theo Dài)");

        // Các tấm vật liệu được sử dụng:
        // #: 1 | Chiều dài: 2440 | Chiều rộng: 1220 | Số lượng: 163 | Vật liệu: "MDF" | Thớ gỗ: ≡ | Nhãn: "Ván MDF 2440x1220" | Giá: 0 | Phần cắt bỏ: Trên 2, Trái 2, Dưới 0, Phải 0 | Mức độ ưu tiên: 0
        AddRepoRow(1, 2440, 1220, 163, "≡ Dọc (Theo Dài)", "Ván MDF 2440x1220", "MDF", 0, 2, 2, 0, 0, 0, 1);

        _numBladeWidth.Value = 3;
        _numOptLevel.Value = 20;
        _numSeed.Value = 1;
        _numMinUseful.Value = 100;
        _cboMethod.SelectedIndex = 0;

        if (showMessage)
        {
            MessageBox.Show(
                "Đã nạp thành công bộ dữ liệu mẫu chuẩn theo hình ảnh!\n\n" +
                "• Chi tiết cần cắt: 8 quy cách, tổng cộng 1.050 chi tiết\n" +
                "  1. 1568 x 953  - SL: 45 (Thớ gỗ dọc)\n" +
                "  2. 754 x 1008  - SL: 45 (Thớ gỗ dọc)\n" +
                "  3. 1284 x 959  - SL: 36 (Thớ gỗ dọc)\n" +
                "  4. 618 x 642   - SL: 96 (Thớ gỗ dọc)\n" +
                "  5. 482 x 340   - SL: 288 (Thớ gỗ dọc)\n" +
                "  6. 779 x 340   - SL: 180 (Thớ gỗ dọc)\n" +
                "  7. 663 x 340   - SL: 270 (Thớ gỗ dọc)\n" +
                "  8. 650 x 340   - SL: 90 (Thớ gỗ dọc)\n\n" +
                "• Các tấm vật liệu được sử dụng: 163 tấm khổ 2440 x 1220 mm\n" +
                "  - Cắt bỏ viền (Trim): Trên 2mm, Trái 2mm, Dưới 0mm, Phải 0mm\n" +
                "  - Mức độ ưu tiên: 0, Giá: 0\n" +
                "• Thớ gỗ: ≡ Cố định dọc theo chiều dài cho toàn bộ các tấm & chi tiết.\n\n" +
                "Bấm '▶ Chạy tối ưu hóa' để tiến hành tính toán và xếp sơ đồ cắt!",
                "Dữ liệu mẫu từ hình ảnh",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }

    private void LoadDefaultExampleData()
    {
        _dgvDemand.Rows.Clear();
        _dgvRepo.Rows.Clear();

        AddDemandRow(1,  60,  40, 2, "🔄 Tự do (Không thớ)", "Phôi A (60x40)", "", true, 0, 0);
        AddDemandRow(2,  48,  40, 1, "🔄 Tự do (Không thớ)", "Phôi B (48x40)", "", true, 1, 0);
        AddDemandRow(3,  60,  53, 1, "🔄 Tự do (Không thớ)", "Phôi C (60x53)", "", true, 2, 0);
        AddDemandRow(4,  45,  45, 1, "🔄 Tự do (Không thớ)", "Phôi D (45x45)", "", true, 3, 0);
        AddDemandRow(5,  50,  80, 1, "🔄 Tự do (Không thớ)", "Phôi E (50x80)", "", true, 4, 0);
        AddDemandRow(6,  45,  75, 1, "🔄 Tự do (Không thớ)", "Phôi F (45x75)", "", true, 5, 0);
        AddDemandRow(7, 100, 100, 2, "🔄 Tự do (Không thớ)", "Cánh tủ (100x100)", "", true, 6, 0);
        AddDemandRow(8,  90,  90, 3, "🔄 Tự do (Không thớ)", "Mặt hộc (90x90)", "", true, 7, 0);
        AddDemandRow(9, 110, 110, 2, "🔄 Tự do (Không thớ)", "Tấm nóc/đáy (110x110)", "", true, 8, 0);

        AddRepoRow(1, 300, 200, 1, "≡ Dọc (Theo Dài)", "Tấm kho #1 (300x200)", "", 0, 0, 0, 0, 0, 0, 0);
        AddRepoRow(2, 200, 200, 1, "≡ Dọc (Theo Dài)", "Tấm kho #2 (200x200)", "", 0, 0, 0, 0, 0, 0, 1);
        AddRepoRow(3, 300, 250, 1, "≡ Dọc (Theo Dài)", "Tấm kho #3 (300x250)", "", 0, 0, 0, 0, 0, 0, 2);

        _numBladeWidth.Value = 0;
        _numOptLevel.Value = 50;
        _numSeed.Value = 1;
        _cboMethod.SelectedIndex = 0;
    }

    /// <summary>
    /// Nạp dữ liệu mẫu minh họa tính năng Thớ gỗ (Grain Direction) và Nhãn chi tiết kèm Số lượng (Quantity)
    /// </summary>
    private void LoadGrainSampleData()
    {
        _dgvDemand.Rows.Clear();
        _dgvRepo.Rows.Clear();

        AddRepoRow(1, 3000, 2000, 2, "≡ Dọc (Theo Dài)", "Ván sồi ghép thanh 3000x2000", "", 0, 0, 0, 0, 0, 0, 100);
        AddRepoRow(2, 2440, 1220, 3, "≡ Dọc (Theo Dài)", "Ván MFC An Cường Sồi 2440x1220", "", 0, 10, 10, 10, 10, 0, 101);

        AddDemandRow(1, 600, 500, 2, "≡ Dọc (Theo Dài)", "Cánh tủ trên", "", false, 1, 0);
        AddDemandRow(2, 500, 600, 3, "||| Ngang (Theo Rộng)", "Đợt tủ vân ngang", "", false, 2, 0);
        AddDemandRow(3, 800, 400, 2, "≡ Dọc (Theo Dài)", "Cánh tủ dài hông", "", false, 3, 0);
        AddDemandRow(4, 400, 700, 1, "||| Ngang (Theo Rộng)", "Mặt bàn trà vân ngang", "", false, 4, 0);
        AddDemandRow(5, 900, 450, 2, "≡ Dọc (Theo Dài)", "Mặt hộc tủ trên", "", false, 5, 0);
        AddDemandRow(6, 700, 350, 2, "🔒 Cố định (Không xoay)", "Vách ngăn đứng", "", false, 6, 0);
        AddDemandRow(7, 600, 600, 2, "🔄 Tự do (Không thớ)", "Tấm lưng tủ (MDF)", "", true, 7, 0);
        AddDemandRow(8, 500, 450, 4, "🔄 Tự do (Không thớ)", "Đáy hộc kéo", "", true, 8, 0);
        AddDemandRow(9, 400, 150, 4, "🔄 Tự do (Không thớ)", "Thanh nẹp hộc kéo", "", true, 9, 0);

        _numBladeWidth.Value = 3;
        _numOptLevel.Value = 60;
        _numSeed.Value = 1;
        _numMinUseful.Value = 100;
        _cboMethod.SelectedIndex = 0;

        MessageBox.Show(
            "Đã nạp bộ dữ liệu mẫu Thớ gỗ & Số lượng (Quantity & Grain Sample)!\n\n" +
            "• Tấm kho: 'Ván sồi 3000x2000' (SL: 2 tấm), 'Ván MFC 2440x1220' (SL: 3 tấm).\n" +
            "• Cánh tủ trên: 600x500 (SL: 2, Thớ dọc)\n" +
            "• Đợt tủ vân ngang: 500x600 (SL: 3, Thớ ngang)\n" +
            "• Đáy hộc kéo: 500x450 (SL: 4, Tự do xoay)\n" +
            "• Thanh nẹp: 400x150 (SL: 4, Tự do xoay)\n\n" +
            "Tổng cộng 22 chi tiết được sinh tự động từ 9 dòng quy cách!\n" +
            "Bấm '▶ Chạy tối ưu hóa' để xem sơ đồ sắp xếp và tính năng gom sơ đồ!",
            "Mẫu Thớ Gỗ & Số Lượng",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    /// <summary>
    /// Nạp dữ liệu mẫu sản xuất hàng loạt (Batch Production) để thấy rõ việc gom các sơ đồ cắt giống hệt nhau
    /// </summary>
    private void LoadBatchSampleData()
    {
        _dgvDemand.Rows.Clear();
        _dgvRepo.Rows.Clear();

        AddRepoRow(1, 2440, 1220, 10, "≡ Dọc (Theo Dài)", "Ván MDF Melamine Sồi 2440x1220", "", 0, 10, 10, 10, 10, 0, 1);

        AddDemandRow(1, 800, 400, 12, "≡ Dọc (Theo Dài)", "Cánh tủ quần áo (800x400)", "", false, 1, 0);
        AddDemandRow(2, 600, 400, 12, "≡ Dọc (Theo Dài)", "Hộc tủ ngăn kéo (600x400)", "", false, 2, 0);
        AddDemandRow(3, 400, 300, 16, "🔄 Tự do (Không thớ)", "Đáy tủ con (400x300)", "", true, 3, 0);

        _numBladeWidth.Value = 3;
        _numOptLevel.Value = 70;
        _numSeed.Value = 1;
        _numMinUseful.Value = 150;
        _cboMethod.SelectedIndex = 0;

        MessageBox.Show(
            "Đã nạp bộ dữ liệu Cắt hàng loạt (Batch Sample)!\n\n" +
            "• Tấm kho: 10 tấm 'Ván MDF 2440x1220'\n" +
            "• Chi tiết: 12 Cánh tủ lớn, 12 Hộc tủ, 16 Đáy tủ (Tổng cộng 40 chi tiết!)\n\n" +
            "Sau khi bấm '▶ Chạy tối ưu hóa':\n" +
            "Hệ thống sẽ gom các tấm có cách sắp xếp giống nhau thành từng Sơ đồ duy nhất\n" +
            "(Ví dụ: Sơ đồ #1 x 3 tấm, Sơ đồ #2 x 2 tấm,...), giúp thợ cưa chỉ cần xem 1 bản vẽ và cắt hàng loạt!",
            "Mẫu Cắt Hàng Loạt",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private void ClearAllData()
    {
        _dgvDemand.Rows.Clear();
        _dgvRepo.Rows.Clear();
        _demandMeta.Clear();
        _repoMeta.Clear();
        _groupedLayouts.Clear();
        _currentDisplayIndex = 0;
        _picCanvas.Invalidate();
    }

    private async void RunOptimization()
    {
        _demandMeta.Clear();
        _repoMeta.Clear();
        _groupedLayouts.Clear();

        // 1. Read Repository Pieces (Expanded by Quantity)
        int totalRepoSheets = 0;
        for (int r = 0; r < _dgvRepo.Rows.Count; r++)
        {
            var row = _dgvRepo.Rows[r];
            if (row.IsNewRow) continue;
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;
            totalRepoSheets += qty;
        }

        if (totalRepoSheets <= 0)
        {
            MessageBox.Show("Vui lòng khai báo ít nhất một tấm kho với số lượng lớn hơn 0!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _opt.NumberOfRepositoryPieces = totalRepoSheets;

        int currentRepoIdx = 0;
        for (int r = 0; r < _dgvRepo.Rows.Count; r++)
        {
            var row = _dgvRepo.Rows[r];
            if (row.IsNewRow) continue;

            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? "";
            string material = row.Cells["Material"].Value?.ToString()?.Trim() ?? "";
            int length = Convert.ToInt32(row.Cells["Length"].Value ?? 0);
            int width = Convert.ToInt32(row.Cells["Width"].Value ?? 0);

            if (!string.IsNullOrEmpty(material) && !string.IsNullOrEmpty(label))
                label = $"{label} ({material})";
            else if (string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(material))
                label = material;
            else if (string.IsNullOrEmpty(label))
                label = $"Ván MDF #{r + 1} ({length}x{width})";
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;

            int top = Convert.ToInt32(row.Cells["TrimTop"].Value ?? 0);
            int left = Convert.ToInt32(row.Cells["TrimLeft"].Value ?? 0);
            int bottom = Convert.ToInt32(row.Cells["TrimBottom"].Value ?? 0);
            int right = Convert.ToInt32(row.Cells["TrimRight"].Value ?? 0);
            int extId = Convert.ToInt32(row.Cells["ExternalID"].Value ?? r + 1);

            string grainStr = row.Cells["SheetGrain"].Value?.ToString() ?? "";
            SheetGrainDirection sheetGrain = grainStr switch
            {
                var s when s.Contains("Ngang") || s.Contains("|||") => SheetGrainDirection.AlongWidth,
                var s when s.Contains("Không thớ") => SheetGrainDirection.None,
                _ => SheetGrainDirection.AlongLength
            };

            for (int k = 0; k < qty; k++)
            {
                _repoMeta.Add(new RepoPieceMeta
                {
                    Length = length,
                    Width = width,
                    Grain = sheetGrain,
                    Label = label,
                    ExternalID = extId
                });

                _opt.SetRepositoryPiece(currentRepoIdx, length, width, top, left, bottom, right, extId, 0, 0, sheetGrain, label);
                currentRepoIdx++;
            }
        }

        // Primary sheet grain (first sheet)
        SheetGrainDirection primarySheetGrain = _repoMeta.Count > 0 ? _repoMeta[0].Grain : SheetGrainDirection.AlongLength;

        // 2. Read Demand Pieces (Expanded by Quantity)
        int totalDemandPieces = 0;
        for (int r = 0; r < _dgvDemand.Rows.Count; r++)
        {
            var row = _dgvDemand.Rows[r];
            if (row.IsNewRow) continue;
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;
            totalDemandPieces += qty;
        }

        if (totalDemandPieces <= 0)
        {
            MessageBox.Show("Vui lòng khai báo ít nhất một chi tiết cần cắt với số lượng lớn hơn 0!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _opt.NumberOfDemandPieces = totalDemandPieces;

        int currentDemandIdx = 0;
        for (int r = 0; r < _dgvDemand.Rows.Count; r++)
        {
            var row = _dgvDemand.Rows[r];
            if (row.IsNewRow) continue;

            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(label)) label = $"Chi tiết #{r + 1}";
            int length = Convert.ToInt32(row.Cells["Length"].Value ?? 0);
            int width = Convert.ToInt32(row.Cells["Width"].Value ?? 0);
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;

            int extId = Convert.ToInt32(row.Cells["ExternalID"].Value ?? r + 1);
            int pri = Convert.ToInt32(row.Cells["Priority"].Value ?? 0);

            string grainStr = row.Cells["Grain"].Value?.ToString() ?? "";
            GrainDirection grain = grainStr switch
            {
                var s when s.Contains("Ngang") || s.Contains("|||") => GrainDirection.AlongWidth,
                var s when s.Contains("Dọc") || s.Contains("≡") => GrainDirection.AlongLength,
                var s when s.Contains("Cố định") || s.Contains("Khóa") => GrainDirection.FixedNoRotate,
                _ => GrainDirection.None
            };

            for (int k = 0; k < qty; k++)
            {
                _demandMeta.Add(new DemandPieceMeta
                {
                    Length = length,
                    Width = width,
                    Grain = grain,
                    ExternalID = extId,
                    Label = label
                });

                _opt.SetDemandPieceWithGrain(currentDemandIdx, length, width, grain, extId, pri, primarySheetGrain, label);
                currentDemandIdx++;
            }
        }

        // 3. Parameters
        _opt.BladeWidth = (int)_numBladeWidth.Value;
        _opt.OptimizationLevel = (int)_numOptLevel.Value;
        _opt.RandomSeed = (int)_numSeed.Value;
        _opt.MinimalUsefulSize = (int)_numMinUseful.Value;
        _opt.MaximalCutLength = (int)_numMaxCutLen.Value;
        _opt.MinimizePatterns = _chkMinimizePatterns?.Checked == true ? 1 : 0;

        // 4. Start optimization
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 30;
        this.Cursor = Cursors.WaitCursor;

        int selectedMethod = _cboMethod.SelectedIndex;

        try
        {
            await Task.Run(() =>
            {
                switch (selectedMethod)
                {
                    case 1:
                        _opt.StartNested();
                        break;
                    case 2:
                        _opt.StartMultiStages();
                        break;
                    default:
                        _opt.StartGuillotine();
                        break;
                }
            });
        }
        finally
        {
            _progressBar.MarqueeAnimationSpeed = 0;
            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Value = 100;
            this.Cursor = Cursors.Default;
        }

        // 5. Group identical cutting patterns
        _groupedLayouts = _opt.GetGroupedSheetLayouts();

        _currentDisplayIndex = 0;
        UpdateUIResults();
    }

    private void UpdateUIResults()
    {
        int totalRawSheets = _opt.TotalNumberOfUtilizedRepositoryPieces;
        int totalPiecesPlaced = _opt.TotalNumberOfUtilizedDemandPieces;
        int requestedPieces = _opt.NumberOfDemandPieces;

        if (totalRawSheets == 0)
        {
            _lblSheetInfo.Text = "Sơ đồ: 0 / 0";
            _lblStats.Text = "Không xếp được chi tiết nào!";
            _btnPrevSheet.Enabled = false;
            _btnNextSheet.Enabled = false;
            _dgvPatterns.Rows.Clear();
            _dgvUtilized.Rows.Clear();
            _dgvCuts.Rows.Clear();
            _dgvWaste.Rows.Clear();
            _picCanvas.Invalidate();
            return;
        }

        // Refresh Summary Patterns Tab
        _dgvPatterns.Rows.Clear();
        for (int g = 0; g < _groupedLayouts.Count; g++)
        {
            var grp = _groupedLayouts[g];
            _dgvPatterns.Rows.Add(
                $"Sơ đồ #{grp.PatternIndex + 1}",
                grp.RepositoryLabel,
                $"{grp.SheetLength} x {grp.SheetWidth}",
                $"{grp.Count} tấm",
                grp.RepresentativeSheet.UtilizedPieces.Count,
                grp.RepresentativeSheet.UtilizedPieces.Count * grp.Count,
                $"{grp.SurfaceCovered:F1}%",
                $"{grp.TotalCutLength:N0}",
                $"{grp.WasteSurface:N0}"
            );
        }

        SheetLayout activeSheet;
        int patternRepeatCount = 1;
        int patternNumber = 1;

        if (_isGroupedMode && _groupedLayouts.Count > 0)
        {
            int totalPatterns = _groupedLayouts.Count;
            _currentDisplayIndex = Math.Clamp(_currentDisplayIndex, 0, totalPatterns - 1);
            var group = _groupedLayouts[_currentDisplayIndex];

            activeSheet = group.RepresentativeSheet;
            patternRepeatCount = group.Count;
            patternNumber = _currentDisplayIndex + 1;

            string repoLabel = group.RepositoryLabel;
            if (string.IsNullOrEmpty(repoLabel)) repoLabel = $"Tấm ván ({group.SheetLength}x{group.SheetWidth})";

            _lblSheetInfo.Text = $"Sơ đồ: {patternNumber} / {totalPatterns}\n[{repoLabel}]\n(Cắt x{patternRepeatCount} tấm)";
            _btnPrevSheet.Text = "◀ Sơ đồ trước";
            _btnNextSheet.Text = "Sơ đồ sau ▶";
            _btnPrevSheet.Enabled = _currentDisplayIndex > 0;
            _btnNextSheet.Enabled = _currentDisplayIndex < totalPatterns - 1;

            _lblStats.Text = $"Sơ đồ #{patternNumber} • Tấm vật liệu: [{repoLabel}] ({activeSheet.SheetLength} x {activeSheet.SheetWidth} mm) | CẮT: {patternRepeatCount} TẤM | Độ phủ: {group.SurfaceCovered:F1}% | Chi tiết: {activeSheet.UtilizedPieces.Count}/tấm (Tổng: {activeSheet.UtilizedPieces.Count * patternRepeatCount})";
        }
        else
        {
            _currentDisplayIndex = Math.Clamp(_currentDisplayIndex, 0, totalRawSheets - 1);
            activeSheet = _opt.GetSheetLayout(_currentDisplayIndex);
            patternRepeatCount = 1;
            patternNumber = _currentDisplayIndex + 1;

            _lblSheetInfo.Text = $"Tấm: {patternNumber} / {totalRawSheets}";
            _btnPrevSheet.Text = "◀ Tấm trước";
            _btnNextSheet.Text = "Tấm sau ▶";
            _btnPrevSheet.Enabled = _currentDisplayIndex > 0;
            _btnNextSheet.Enabled = _currentDisplayIndex < totalRawSheets - 1;

            _lblStats.Text = $"Tấm #{patternNumber}: [{activeSheet.RepositoryLabel}] | Độ phủ: {activeSheet.SurfaceCovered:F1}% | Chi tiết: {activeSheet.UtilizedPieces.Count}";
        }

        // Sheet grain for this layout
        SheetGrainDirection sheetGrain = _opt.GetSheetGrain(activeSheet.SheetIndex);

        // Populate Utilized Pieces table
        _dgvUtilized.Rows.Clear();
        for (int i = 0; i < activeSheet.UtilizedPieces.Count; i++)
        {
            var p = activeSheet.UtilizedPieces[i];
            int tx = p.TopX, ty = p.TopY, bx = p.BottomX, by = p.BottomY;
            int w = p.Width;
            int h = p.Height;
            int rot = p.Rotated;
            int demIdx = p.DemandIndex;
            int demId = p.DemandExternalID;

            string pieceLabel = p.Label;
            if (string.IsNullOrEmpty(pieceLabel) && demIdx >= 0 && demIdx < _demandMeta.Count)
            {
                pieceLabel = _demandMeta[demIdx].Label;
            }
            if (string.IsNullOrEmpty(pieceLabel))
            {
                pieceLabel = $"Chi tiết #{demId}";
            }

            // Grain info
            string grainLabel = "⟳ Tự do (Không thớ)";
            if (demIdx >= 0 && demIdx < _demandMeta.Count)
            {
                var meta = _demandMeta[demIdx];
                if (meta.Grain == GrainDirection.AlongLength)
                {
                    grainLabel = "⟷ Dọc (Thuận thớ Dài)";
                }
                else if (meta.Grain == GrainDirection.AlongWidth)
                {
                    grainLabel = "↕ Ngang (Thuận thớ Rộng)";
                }
                else if (meta.Grain == GrainDirection.FixedNoRotate)
                {
                    grainLabel = "🔒 Cố định không xoay";
                }
            }

            string rotLabel = rot == 1 ? "90° (Đã xoay)" : "0° (Nguyên)";
            _dgvUtilized.Rows.Add(
                i + 1,
                pieceLabel,
                $"({tx}, {ty}) - ({bx}, {by})",
                $"{w} x {h}",
                "1",
                patternRepeatCount.ToString(),
                grainLabel,
                rotLabel,
                demId
            );
        }

        // Populate Cuts table
        _dgvCuts.Rows.Clear();
        for (int i = 0; i < activeSheet.Cuts.Count; i++)
        {
            var c = activeSheet.Cuts[i];
            string dir = (c.TopX == c.BottomX) ? "Dọc" : (c.TopY == c.BottomY) ? "Ngang" : "Khác";
            _dgvCuts.Rows.Add(i + 1, $"({c.TopX}, {c.TopY}) -> ({c.BottomX}, {c.BottomY})", dir, c.CutThickness, c.Level > 0 ? c.Level.ToString() : "-");
        }

        // Populate Waste table
        _dgvWaste.Rows.Clear();
        for (int i = 0; i < activeSheet.WastePieces.Count; i++)
        {
            var w = activeSheet.WastePieces[i];
            int ww = w.Width;
            int wh = w.Height;
            bool isReusable = (ww >= _opt.MinimalUsefulSize && wh >= _opt.MinimalUsefulSize);
            string cls = isReusable ? "Tái sử dụng" : "Vụn";
            string grainAvail = isReusable
                ? (sheetGrain == SheetGrainDirection.AlongLength ? "⟷ Dọc (Dài theo X)" : "↕ Ngang (Rộng theo Y)")
                : "-";
            _dgvWaste.Rows.Add(
                i + 1,
                $"({w.TopX}, {w.TopY}) - ({w.BottomX}, {w.BottomY})",
                $"{ww} x {wh}",
                w.Area,
                w.Area * patternRepeatCount,
                cls,
                grainAvail
            );
        }

        _picCanvas.Invalidate();
    }

    private void PicCanvas_Paint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_opt.TotalNumberOfUtilizedRepositoryPieces == 0)
        {
            using var font = new Font("Segoe UI", 12f);
            using var brush = new SolidBrush(Color.Gray);
            g.DrawString("Chưa có dữ liệu bản vẽ. Vui lòng bấm 'Bắt đầu tối ưu hóa'.", font, brush, 30, 30);
            return;
        }

        SheetLayout activeSheet;
        int patternRepeatCount = 1;
        int patternNumber = 1;

        if (_isGroupedMode && _groupedLayouts.Count > 0)
        {
            int groupIdx = Math.Clamp(_currentDisplayIndex, 0, _groupedLayouts.Count - 1);
            var group = _groupedLayouts[groupIdx];
            activeSheet = group.RepresentativeSheet;
            patternRepeatCount = group.Count;
            patternNumber = groupIdx + 1;
        }
        else
        {
            int sheetIdx = Math.Clamp(_currentDisplayIndex, 0, _opt.TotalNumberOfUtilizedRepositoryPieces - 1);
            activeSheet = _opt.GetSheetLayout(sheetIdx);
            patternRepeatCount = 1;
            patternNumber = sheetIdx + 1;
        }

        if (activeSheet == null || activeSheet.SheetLength <= 0 || activeSheet.SheetWidth <= 0) return;

        int sheetW = activeSheet.SheetLength;
        int sheetH = activeSheet.SheetWidth;
        string sheetLabel = activeSheet.RepositoryLabel;
        if (string.IsNullOrEmpty(sheetLabel))
        {
            sheetLabel = $"Tấm kho #{activeSheet.RepositoryIndex + 1}";
        }

        SheetGrainDirection sheetGrain = _opt.GetSheetGrain(activeSheet.SheetIndex);

        // Calculate scaling & margins
        int canvasW = _picCanvas.ClientSize.Width;
        int canvasH = _picCanvas.ClientSize.Height;

        int margin = 50;
        float availW = canvasW - margin * 2;
        float availH = canvasH - margin * 2;

        float scale = Math.Min(availW / sheetW, availH / sheetH);
        float originX = margin + (availW - sheetW * scale) / 2f;
        float originY = margin + (availH - sheetH * scale) / 2f;

        // 1. Draw Stock Sheet Background (Wood tone)
        var sheetRect = new RectangleF(originX, originY, sheetW * scale, sheetH * scale);
        using (var sheetBrush = new SolidBrush(Color.FromArgb(250, 246, 238)))
        using (var sheetPen = new Pen(Color.FromArgb(50, 50, 50), 2f))
        {
            g.FillRectangle(sheetBrush, sheetRect);

            // Subtle Sheet Wood Grain Lines
            if (sheetGrain == SheetGrainDirection.AlongLength)
            {
                using var grainPen = new Pen(Color.FromArgb(20, 160, 110, 50), 1f);
                for (float y = originY + 12; y < originY + sheetH * scale; y += 14)
                {
                    g.DrawLine(grainPen, originX, y, originX + sheetW * scale, y);
                }
            }
            else if (sheetGrain == SheetGrainDirection.AlongWidth)
            {
                using var grainPen = new Pen(Color.FromArgb(20, 160, 110, 50), 1f);
                for (float x = originX + 12; x < originX + sheetW * scale; x += 14)
                {
                    g.DrawLine(grainPen, x, originY, x, originY + sheetH * scale);
                }
            }

            g.DrawRectangle(sheetPen, sheetRect.X, sheetRect.Y, sheetRect.Width, sheetRect.Height);
        }

        // Draw Sheet dimensions & Sheet Label Badge
        using (var dimFont = new Font("Segoe UI", 9f, FontStyle.Bold))
        using (var dimBrush = new SolidBrush(Color.FromArgb(60, 70, 85)))
        {
            string dimTop = $"{sheetW} mm (Dài)";
            var sizeTop = g.MeasureString(dimTop, dimFont);
            g.DrawString(dimTop, dimFont, dimBrush, originX + (sheetW * scale - sizeTop.Width) / 2f, originY - 22);

            string dimLeft = $"{sheetH} mm\n(Rộng)";
            var sizeLeft = g.MeasureString(dimLeft, dimFont);
            g.DrawString(dimLeft, dimFont, dimBrush, originX - sizeLeft.Width - 10, originY + (sheetH * scale - sizeLeft.Height) / 2f);

            // Sheet Label & Grain Badge at top-left
            string sheetGrainText = sheetGrain switch
            {
                SheetGrainDirection.AlongLength => "⟷ Thớ dọc (Length)",
                SheetGrainDirection.AlongWidth => "↕ Thớ ngang (Width)",
                _ => "Không thớ"
            };
            string sheetTitleBadge = $"🪵 Sơ đồ #{patternNumber} • Tấm vật liệu: [{sheetLabel}] ({sheetW} x {sheetH} mm) • {sheetGrainText}";

            using var badgeBgBrush = new SolidBrush(Color.FromArgb(235, 220, 195));
            using var badgeBorderPen = new Pen(Color.FromArgb(180, 140, 90), 1f);
            using var badgeTextBrush = new SolidBrush(Color.FromArgb(90, 50, 10));
            using var badgeFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            var szBadge = g.MeasureString(sheetTitleBadge, badgeFont);
            float bx = originX + 8;
            float by = originY + 8;
            var badgeRect = new RectangleF(bx, by, szBadge.Width + 12, szBadge.Height + 6);
            g.FillRoundedRectangle(badgeBgBrush, badgeRect, 4);
            g.DrawRoundedRectangle(badgeBorderPen, badgeRect, 4);
            g.DrawString(sheetTitleBadge, badgeFont, badgeTextBrush, bx + 6, by + 3);

            // BIG BOLD STAMP AT TOP-RIGHT: "✂ CẮT x4 TẤM"
            if (_isGroupedMode && patternRepeatCount > 1)
            {
                string cutStamp = $"✂ CẮT x{patternRepeatCount} TẤM (Lặp {patternRepeatCount} lần)";
                using var stampFont = new Font("Segoe UI", 10.5f, FontStyle.Bold);
                using var stampBg = new SolidBrush(Color.FromArgb(235, 16, 124, 65)); // Professional Green
                using var stampTextBrush = new SolidBrush(Color.White);
                using var stampBorder = new Pen(Color.FromArgb(10, 90, 45), 1.5f);

                var szStamp = g.MeasureString(cutStamp, stampFont);
                float sx = originX + sheetW * scale - szStamp.Width - 14;
                float sy = originY + 8;
                var stampRect = new RectangleF(sx, sy, szStamp.Width + 12, szStamp.Height + 6);

                g.FillRoundedRectangle(stampBg, stampRect, 5);
                g.DrawRoundedRectangle(stampBorder, stampRect, 5);
                g.DrawString(cutStamp, stampFont, stampTextBrush, sx + 6, sy + 3);
            }
        }

        // 2. Draw Waste Pieces (Hatched / Gạch chéo / Light Orange)
        Color hatchForeColor = Color.FromArgb(215, 160, 110);
        Color hatchBackColor = Color.FromArgb(254, 243, 230);

        using (var wastePen = new Pen(Color.FromArgb(210, 150, 90), 1f) { DashStyle = DashStyle.Dash })
        using (var wasteFont = new Font("Segoe UI", 8f, FontStyle.Italic))
        using (var wasteTextBrush = new SolidBrush(Color.FromArgb(160, 80, 20)))
        using (var cornerXPen = new Pen(hatchForeColor, 1.2f))
        using (var solidWasteBrush = new SolidBrush(Color.FromArgb(254, 237, 216)))
        {
            for (int i = 0; i < activeSheet.WastePieces.Count; i++)
            {
                var w = activeSheet.WastePieces[i];
                float rx = originX + w.TopX * scale;
                float ry = originY + w.TopY * scale;
                float rw = w.Width * scale;
                float rh = w.Height * scale;

                if (rw <= 0 || rh <= 0) continue;

                var rect = new RectangleF(rx, ry, rw, rh);

                // Tô phần phế liệu theo kiểu gạch chéo
                switch (_wasteHatchStyle)
                {
                    case WasteHatchStyle.DiagonalCross:
                        using (var hb = new HatchBrush(HatchStyle.DiagonalCross, hatchForeColor, hatchBackColor))
                            g.FillRectangle(hb, rect);
                        break;

                    case WasteHatchStyle.BackwardDiagonal:
                        using (var hb = new HatchBrush(HatchStyle.BackwardDiagonal, hatchForeColor, hatchBackColor))
                            g.FillRectangle(hb, rect);
                        break;

                    case WasteHatchStyle.ForwardDiagonal:
                        using (var hb = new HatchBrush(HatchStyle.ForwardDiagonal, hatchForeColor, hatchBackColor))
                            g.FillRectangle(hb, rect);
                        break;

                    case WasteHatchStyle.CornerX:
                        g.FillRectangle(solidWasteBrush, rect);
                        if (rw > 4 && rh > 4)
                        {
                            g.DrawLine(cornerXPen, rect.Left, rect.Top, rect.Right, rect.Bottom);
                            g.DrawLine(cornerXPen, rect.Left, rect.Bottom, rect.Right, rect.Top);
                        }
                        break;

                    case WasteHatchStyle.Solid:
                    default:
                        g.FillRectangle(solidWasteBrush, rect);
                        break;
                }

                // Viền nét đứt của mảnh phế liệu
                g.DrawRectangle(wastePen, rect.X, rect.Y, rect.Width, rect.Height);

                // Hiển thị nhãn kích thước phế liệu kèm khung mờ nổi bật trên nền gạch chéo
                if (rw > 28 && rh > 20)
                {
                    string label = $"w\n{w.Width}x{w.Height}";
                    var sz = g.MeasureString(label, wasteFont);
                    float lx = rx + (rw - sz.Width) / 2f;
                    float ly = ry + (rh - sz.Height) / 2f;

                    var badgeRect = new RectangleF(lx - 4, ly - 2, sz.Width + 8, sz.Height + 4);
                    using var labelBg = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
                    using var labelBorder = new Pen(Color.FromArgb(225, 185, 140), 1f);
                    g.FillRoundedRectangle(labelBg, badgeRect, 3);
                    g.DrawRoundedRectangle(labelBorder, badgeRect, 3);
                    g.DrawString(label, wasteFont, wasteTextBrush, lx, ly);
                }
            }
        }

        // 3. Draw Utilized Demand Pieces (Soft Colors + Crisp border + Dimensions + Label + Grain texture)
        Color[] pieceColors = new Color[]
        {
            Color.FromArgb(218, 232, 252), // Blue
            Color.FromArgb(213, 232, 212), // Green
            Color.FromArgb(248, 206, 204), // Red/Pink
            Color.FromArgb(225, 213, 231), // Purple
            Color.FromArgb(255, 230, 204), // Peach
            Color.FromArgb(255, 242, 204)  // Yellow
        };

        using (var piecePen = new Pen(Color.FromArgb(35, 40, 50), 1.5f))
        using (var labelFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
        using (var subFont = new Font("Segoe UI", 7.5f))
        using (var textBrush = new SolidBrush(Color.FromArgb(20, 25, 35)))
        {
            for (int i = 0; i < activeSheet.UtilizedPieces.Count; i++)
            {
                var p = activeSheet.UtilizedPieces[i];
                float rx = originX + p.TopX * scale;
                float ry = originY + p.TopY * scale;
                float rw = p.Width * scale;
                float rh = p.Height * scale;

                var rect = new RectangleF(rx, ry, rw, rh);
                Color color = pieceColors[p.DemandIndex % pieceColors.Length];

                using (var brush = new SolidBrush(color))
                {
                    g.FillRectangle(brush, rect);
                }

                // Determine grain direction and label of this placed piece
                GrainDirection pieceGrain = GrainDirection.None;
                string pieceLabel = p.Label;

                if (p.DemandIndex >= 0 && p.DemandIndex < _demandMeta.Count)
                {
                    pieceGrain = _demandMeta[p.DemandIndex].Grain;
                    if (string.IsNullOrEmpty(pieceLabel))
                        pieceLabel = _demandMeta[p.DemandIndex].Label;
                }
                if (string.IsNullOrEmpty(pieceLabel))
                {
                    pieceLabel = $"#{p.DemandExternalID}";
                }

                // Draw Wood Grain Texture Lines on Demand Piece if grain is defined
                if (pieceGrain != GrainDirection.None)
                {
                    using var grainPen = new Pen(Color.FromArgb(35, 0, 0, 0), 1f) { DashStyle = DashStyle.Dash };
                    for (float gy = ry + 8; gy < ry + rh - 4; gy += 10)
                    {
                        g.DrawLine(grainPen, rx + 4, gy, rx + rw - 4, gy);
                    }
                }

                g.DrawRectangle(piecePen, rect.X, rect.Y, rect.Width, rect.Height);

                // Dimension, ID, Label, and Grain indicator text
                if (rw > 32 && rh > 22)
                {
                    string grainSymbol = pieceGrain switch
                    {
                        GrainDirection.AlongLength => "[≡ Dọc]",
                        GrainDirection.AlongWidth => "[||| Ngang]",
                        GrainDirection.FixedNoRotate => "[🔒 Khóa]",
                        _ => (p.Rotated == 1 ? "[⟳ Xoay 90°]" : "[⟳ Tự do]")
                    };

                    int pw = p.Width;
                    int ph = p.Height;
                    string line1 = pieceLabel;
                    string line2 = $"{pw}x{ph} {grainSymbol}";

                    var sz1 = g.MeasureString(line1, labelFont);
                    var sz2 = g.MeasureString(line2, subFont);

                    // Check if both lines fit comfortably
                    if (sz1.Height + sz2.Height + 4 < rh && Math.Max(sz1.Width, sz2.Width) < rw)
                    {
                        float totalH = sz1.Height + sz2.Height;
                        float startTextY = ry + (rh - totalH) / 2f;

                        g.DrawString(line1, labelFont, textBrush, rx + (rw - sz1.Width) / 2f, startTextY);

                        using var grainBrush = new SolidBrush(pieceGrain != GrainDirection.None ? Color.FromArgb(120, 50, 10) : Color.FromArgb(70, 75, 85));
                        g.DrawString(line2, subFont, grainBrush, rx + (rw - sz2.Width) / 2f, startTextY + sz1.Height);
                    }
                    else if (sz1.Width < rw && sz1.Height < rh)
                    {
                        // Fit single line label
                        g.DrawString(line1, labelFont, textBrush, rx + (rw - sz1.Width) / 2f, ry + (rh - sz1.Height) / 2f);
                    }
                    else
                    {
                        // Compact fallback: just dimensions
                        string shortTitle = $"{pw}x{ph}";
                        var szShort = g.MeasureString(shortTitle, subFont);
                        if (szShort.Width < rw && szShort.Height < rh)
                        {
                            g.DrawString(shortTitle, subFont, textBrush, rx + (rw - szShort.Width) / 2f, ry + (rh - szShort.Height) / 2f);
                        }
                    }
                }
            }
        }

        // 4. Draw Guillotine Cuts (Red Lines + Sequence Badges)
        using (var cutPen = new Pen(Color.FromArgb(220, 20, 60), 2f))
        using (var badgeBrush = new SolidBrush(Color.FromArgb(220, 20, 60)))
        using (var badgeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
        using (var badgeTextBrush = new SolidBrush(Color.White))
        {
            for (int i = 0; i < activeSheet.Cuts.Count; i++)
            {
                var c = activeSheet.Cuts[i];

                float p1x = originX + c.TopX * scale;
                float p1y = originY + c.TopY * scale;
                float p2x = originX + c.BottomX * scale;
                float p2y = originY + c.BottomY * scale;

                g.DrawLine(cutPen, p1x, p1y, p2x, p2y);

                // Draw small sequence number badge near the middle of cut
                float midX = (p1x + p2x) / 2f;
                float midY = (p1y + p2y) / 2f;

                string badge = (i + 1).ToString();
                var sz = g.MeasureString(badge, badgeFont);
                float brw = Math.Max(16, sz.Width + 4);
                float brh = 16;
                var badgeRect = new RectangleF(midX - brw / 2f, midY - brh / 2f, brw, brh);

                g.FillEllipse(badgeBrush, badgeRect);
                g.DrawString(badge, badgeFont, badgeTextBrush, badgeRect.X + (brw - sz.Width) / 2f, badgeRect.Y + (brh - sz.Height) / 2f);
            }
        }
    }

    private void ShowGrainHelp()
    {
        string helpMsg =
            "══════════════════════════════════════════════════════════════════════\n" +
            "  HƯỚNG DẪN SỐ LƯỢNG, THỚ GỖ & GOM SƠ ĐỒ CẮT HÀNG LOẠT\n" +
            "  Tham khảo: https://www.optimalprograms.com/help/optimization2dx-4/files/grain_direction.htm\n" +
            "══════════════════════════════════════════════════════════════════════\n\n" +
            "1. GOM CÁC SƠ ĐỒ GIỐNG NHAU (PATTERN GROUPING):\n" +
            "   • Khi cắt hàng loạt hoặc khi chi tiết có số lượng lớn, thuật toán sẽ sinh ra nhiều tấm có sơ đồ giống hệt nhau.\n" +
            "   • Hệ thống tự động phát hiện và gom các tấm cùng mẫu thành một 'Sơ đồ' duy nhất kèm số lượng cắt (ví dụ: Sơ đồ #1 x 4 tấm).\n" +
            "   • Thợ cưa chỉ cần xem một bản vẽ duy nhất và biết chính xác số lần cần cưa lặp lại, tiết kiệm thời gian và tránh nhầm lẫn.\n" +
            "   • Bạn có thể xem bảng '📊 Tổng hợp sơ đồ (Patterns)' và nhấp đúp vào dòng để chuyển nhanh đến sơ đồ đó.\n\n" +
            "2. SỐ LƯỢNG (QUANTITY):\n" +
            "   • Nhập một dòng duy nhất cho mỗi quy cách (Dài x Rộng) kèm cột 'Số lượng (SL)'.\n" +
            "   • Với Tấm kho: Nhập số lượng tấm phôi sẵn có trong kho.\n\n" +
            "3. NGUYÊN TẮC THỚ GỖ (GRAIN DIRECTION):\n" +
            "   • Tấm kho nguyên liệu (Repository): mặc định thớ gỗ chạy dọc theo chiều Dài (Length).\n" +
            "   • Chi tiết cần cắt (Demand): cạnh nào muốn song song với chiều thớ gỗ thì cạnh đó được coi là Length.\n" +
            "   • Khóa góc xoay (CanRotate = 0) để đảm bảo chi tiết luôn thuận thớ 100%.\n";

        MessageBox.Show(helpMsg, "Hướng dẫn Gom Sơ Đồ & Thớ Gỗ", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportResults()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "Cut2DX_Result.txt"
        };
        if (sfd.ShowDialog() == DialogResult.OK)
        {
            using var sw = new StreamWriter(sfd.FileName);
            sw.WriteLine("// ================================================================");
            sw.WriteLine("// BÁO CÁO KẾT QUẢ TỐI ƯU HÓA SƠ ĐỒ CẮT 2D (CUT2DX C# EDITION)");
            sw.WriteLine($"// Thời gian xuất: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sw.WriteLine("// ================================================================");
            sw.WriteLine();

            sw.WriteLine("=== DANH SÁCH QUY CÁCH YÊU CẦU (DEMAND SPECIFICATIONS) ===");
            for (int r = 0; r < _dgvDemand.Rows.Count; r++)
            {
                var row = _dgvDemand.Rows[r];
                if (row.IsNewRow) continue;
                string lbl = row.Cells["Label"].Value?.ToString();
                int l = Convert.ToInt32(row.Cells["Length"].Value ?? 0);
                int w = Convert.ToInt32(row.Cells["Width"].Value ?? 0);
                int q = Convert.ToInt32(row.Cells["Quantity"].Value ?? 1);
                string gr = row.Cells["Grain"].Value?.ToString();
                sw.WriteLine($"  [{r + 1}] {lbl,-25} | Kích thước: {l,4} x {w,4} mm | SL: {q,3} | Thớ gỗ: {gr}");
            }
            sw.WriteLine();

            int totalSheets = _opt.TotalNumberOfUtilizedRepositoryPieces;
            sw.WriteLine($"Tổng số tấm ván cần cắt: {totalSheets} tấm");
            sw.WriteLine($"Tổng số mẫu sơ đồ khác nhau: {_groupedLayouts.Count} sơ đồ");
            sw.WriteLine($"Tổng số chi tiết xếp được: {_opt.TotalNumberOfUtilizedDemandPieces} / {_opt.NumberOfDemandPieces}");
            sw.WriteLine();

            sw.WriteLine("================================================================");
            sw.WriteLine("TỔNG HỢP CÁC SƠ ĐỒ CẮT (GOM NHÓM SƠ ĐỒ GIỐNG NHAU)");
            sw.WriteLine("================================================================");
            for (int g = 0; g < _groupedLayouts.Count; g++)
            {
                var grp = _groupedLayouts[g];
                sw.WriteLine($"  SƠ ĐỒ #{g + 1,-2} -> CẮT: {grp.Count,2} TẤM | Tấm kho: [{grp.RepositoryLabel}] ({grp.SheetLength}x{grp.SheetWidth} mm) | Độ phủ: {grp.SurfaceCovered:F1}% | Chi tiết: {grp.RepresentativeSheet.UtilizedPieces.Count}/tấm (Tổng: {grp.RepresentativeSheet.UtilizedPieces.Count * grp.Count})");
            }
            sw.WriteLine();

            sw.WriteLine("================================================================");
            sw.WriteLine("CHI TIẾT TỪNG MẪU SƠ ĐỒ CẮT");
            sw.WriteLine("================================================================");

            for (int g = 0; g < _groupedLayouts.Count; g++)
            {
                var grp = _groupedLayouts[g];
                var s = grp.RepresentativeSheet;

                sw.WriteLine($"----------------------------------------------------------------");
                sw.WriteLine($"MẪU SƠ ĐỒ #{g + 1}: CẮT {grp.Count} TẤM - [{grp.RepositoryLabel}] ({grp.SheetLength} x {grp.SheetWidth} mm)");
                sw.WriteLine($"  - Độ phủ hữu dụng: {grp.SurfaceCovered:F2}%");
                sw.WriteLine($"  - Tổng chiều dài mạch cắt trên 1 tấm: {grp.TotalCutLength:N1} mm");
                sw.WriteLine($"  - Số chi tiết / tấm: {s.UtilizedPieces.Count} (Tổng chi tiết cắt theo sơ đồ này: {s.UtilizedPieces.Count * grp.Count})");
                sw.WriteLine($"  - Số nhát cắt: {s.Cuts.Count} | Số mảnh phế liệu: {s.WastePieces.Count}");
                sw.WriteLine();

                sw.WriteLine("  [CHI TIẾT TRÊN SƠ ĐỒ]:");
                for (int p = 0; p < s.UtilizedPieces.Count; p++)
                {
                    var pc = s.UtilizedPieces[p];
                    string pieceLabel = pc.Label;
                    if (string.IsNullOrEmpty(pieceLabel) && pc.DemandIndex >= 0 && pc.DemandIndex < _demandMeta.Count)
                        pieceLabel = _demandMeta[pc.DemandIndex].Label;

                    sw.WriteLine($"    {p + 1,2}. [{pieceLabel}] Tọa độ: ({pc.TopX,5},{pc.TopY,5}) -> ({pc.BottomX,5},{pc.BottomY,5}) | Kích thước: {pc.Width,4} x {pc.Height,4} | Xoay: {(pc.Rotated == 1 ? "90°" : "0°")} | SL/tấm: 1 | Tổng SL: {grp.Count}");
                }
                sw.WriteLine();

                sw.WriteLine("  [NHÁT CẮT GUILLOTINE]:");
                for (int c = 0; c < s.Cuts.Count; c++)
                {
                    var ct = s.Cuts[c];
                    string dir = (ct.TopX == ct.BottomX) ? "Dọc" : (ct.TopY == ct.BottomY) ? "Ngang" : "Khác";
                    sw.WriteLine($"    Nhát {c + 1,2}: ({ct.TopX,5},{ct.TopY,5}) -> ({ct.BottomX,5},{ct.BottomY,5}) | Hướng: {dir,-5} | Độ dày: {ct.CutThickness} | Tầng: {ct.Level}");
                }
                sw.WriteLine();

                sw.WriteLine("  [PHẾ LIỆU]:");
                for (int w = 0; w < s.WastePieces.Count; w++)
                {
                    var wt = s.WastePieces[w];
                    bool isReusable = (wt.Width >= _opt.MinimalUsefulSize && wt.Height >= _opt.MinimalUsefulSize);
                    sw.WriteLine($"    Mảnh {w + 1,2}: ({wt.TopX,5},{wt.TopY,5}) -> ({wt.BottomX,5},{wt.BottomY,5}) | Kích thước: {wt.Width,4} x {wt.Height,4} | DT 1 tấm: {wt.Area,7} | Tổng DT: {wt.Area * grp.Count,8} | Loại: {(isReusable ? "Tái sử dụng" : "Vụn")}");
                }
                sw.WriteLine();
            }

            MessageBox.Show("Đã lưu kết quả chi tiết kèm Báo cáo gom nhóm sơ đồ cắt thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ExportHtmlReport()
    {
        if (_opt.TotalNumberOfUtilizedRepositoryPieces == 0)
        {
            MessageBox.Show("Vui lòng bấm '▶ Bắt đầu tối ưu hóa' trước khi xuất báo cáo HTML!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*",
            FileName = $"Cut2DX_BaoCaoSoDo_{DateTime.Now:yyyyMMdd_HHmmss}.html",
            Title = "Lưu Báo Cáo Sơ Đồ Cắt 2D (HTML)"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                HtmlReportExporter.Export(
                    _opt,
                    _groupedLayouts,
                    _dgvDemand,
                    _dgvRepo,
                    (int)_numBladeWidth.Value,
                    (int)_numMinUseful.Value,
                    sfd.FileName
                );

                var res = MessageBox.Show(
                    $"Đã xuất Báo cáo Sơ đồ cắt sang HTML thành công!\n\nĐường dẫn: {sfd.FileName}\n\nBạn có muốn mở xem ngay trên trình duyệt web không?",
                    "Xuất HTML thành công",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (res == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file HTML: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public void RunAndExportHtmlHeadless(string outputPath)
    {
        // 1. Read Repo
        int totalRepoSheets = 0;
        for (int r = 0; r < _dgvRepo.Rows.Count; r++)
        {
            var row = _dgvRepo.Rows[r];
            if (row.IsNewRow) continue;
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;
            totalRepoSheets += qty;
        }
        _opt.NumberOfRepositoryPieces = totalRepoSheets;
        int currentRepoIdx = 0;
        for (int r = 0; r < _dgvRepo.Rows.Count; r++)
        {
            var row = _dgvRepo.Rows[r];
            if (row.IsNewRow) continue;
            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? "";
            string material = row.Cells["Material"].Value?.ToString()?.Trim() ?? "";
            int length = Convert.ToInt32(row.Cells["Length"].Value ?? 0);
            int width = Convert.ToInt32(row.Cells["Width"].Value ?? 0);

            if (!string.IsNullOrEmpty(material) && !string.IsNullOrEmpty(label))
                label = $"{label} ({material})";
            else if (string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(material))
                label = material;
            else if (string.IsNullOrEmpty(label))
                label = $"Ván MDF #{r + 1} ({length}x{width})";
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;
            int top = Convert.ToInt32(row.Cells["TrimTop"].Value ?? 0);
            int left = Convert.ToInt32(row.Cells["TrimLeft"].Value ?? 0);
            int bottom = Convert.ToInt32(row.Cells["TrimBottom"].Value ?? 0);
            int right = Convert.ToInt32(row.Cells["TrimRight"].Value ?? 0);
            int extId = Convert.ToInt32(row.Cells["ExternalID"].Value ?? r + 1);
            for (int k = 0; k < qty; k++)
            {
                _opt.SetRepositoryPiece(currentRepoIdx, length, width, top, left, bottom, right, extId, 0, 0, SheetGrainDirection.AlongLength, label);
                currentRepoIdx++;
            }
        }

        // 2. Read Demand
        int totalDemandPieces = 0;
        for (int r = 0; r < _dgvDemand.Rows.Count; r++)
        {
            var row = _dgvDemand.Rows[r];
            if (row.IsNewRow) continue;
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;
            totalDemandPieces += qty;
        }
        _opt.NumberOfDemandPieces = totalDemandPieces;
        int currentDemandIdx = 0;
        for (int r = 0; r < _dgvDemand.Rows.Count; r++)
        {
            var row = _dgvDemand.Rows[r];
            if (row.IsNewRow) continue;
            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? "";
            string material = row.Cells["Material"].Value?.ToString()?.Trim() ?? "";
            int length = Convert.ToInt32(row.Cells["Length"].Value ?? 0);
            int width = Convert.ToInt32(row.Cells["Width"].Value ?? 0);

            if (!string.IsNullOrEmpty(material) && !string.IsNullOrEmpty(label))
                label = $"{label} ({material})";
            else if (string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(material))
                label = material;
            else if (string.IsNullOrEmpty(label))
                label = $"Ván MDF #{r + 1} ({length}x{width})";
            int qty = 1;
            if (int.TryParse(row.Cells["Quantity"].Value?.ToString(), out int parsedQty) && parsedQty > 0)
                qty = parsedQty;
            int extId = Convert.ToInt32(row.Cells["ExternalID"].Value ?? r + 1);
            for (int k = 0; k < qty; k++)
            {
                _opt.SetDemandPieceWithGrain(currentDemandIdx, length, width, GrainDirection.AlongLength, extId, 0, SheetGrainDirection.AlongLength, label);
                currentDemandIdx++;
            }
        }

        _opt.BladeWidth = 3;
        _opt.OptimizationLevel = 5;
        _opt.MinimizePatterns = 1;
        _opt.StartGuillotine();

        _groupedLayouts = _opt.GetGroupedSheetLayouts();

        HtmlReportExporter.Export(
            _opt,
            _groupedLayouts,
            _dgvDemand,
            _dgvRepo,
            _opt.BladeWidth,
            (int)_numMinUseful.Value,
            outputPath
        );
    }
}

// Extension methods for rounded rectangles
public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF rect, float radius)
    {
        using var path = CreateRoundedPath(rect, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, RectangleF rect, float radius)
    {
        using var path = CreateRoundedPath(rect, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
