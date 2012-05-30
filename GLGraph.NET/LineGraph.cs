﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using Size = System.Drawing.Size;

namespace GLGraph.NET {

    public class LineChangedEventArgs : EventArgs {
        public Line Line { get; set; }
        public LineChangedEventArgs(Line line) {
            Line = line;
        }
    }

    public class Line {
        public event EventHandler<LineChangedEventArgs> Changed;

        public List<GLPoint> Points { get; set; }
        public GLColor Color { get; set; }
        public float Thickness { get; set; }

        public bool IsDynamic { get; set; }

        public Line(float thickness, GLColor color, GLPoint[] points) {
            var copy = new List<GLPoint>();
            copy.AddRange(points);

            Color = color;
            Thickness = thickness;
            Points = copy;
        }

        public void AddPoint(GLPoint point, bool update = true) {
            Points.Add(point);
            if (update) {
                if (Changed != null) {
                    Changed(this, new LineChangedEventArgs(this));
                }
            }
        }

        public void RemovePoint(int index, bool update = true) {
            Points.RemoveAt(index);
            if (update) {
                if (Changed != null) {
                    Changed(this, new LineChangedEventArgs(this));
                }
            }
        }
    }

    public class GraphWindow {
        public GLPoint DataOrigin { get; set; }
        public double DataWidth { get; set; }
        public double DataHeight { get; set; }
        public int WindowHeight { get; set; }
        public int WindowWidth { get; set; }

        public double Top {
            get { return DataOrigin.Y + DataHeight; }
        }

        public double Finish {
            get { return DataOrigin.X + DataWidth; }
        }

        public double Start {
            get { return DataOrigin.X; }
        }

        public double Bottom {
            get { return DataOrigin.Y; }
        }

        public GLPoint ScreenToView(GLPoint location) {
            //GL.Scale(1.0 / WindowWidth, 1.0 / WindowHeight, 1.0);
            //GL.Translate(50, 50, 0);
            //GL.Scale(WindowWidth, WindowHeight, 1.0);

            //GL.Scale(1.0 / DataWidth, 1.0 / DataHeight, 0);
            //GL.Translate(-DataOrigin.X, -DataOrigin.Y, 0);

            var x = location.X;
            var y = location.Y;

            y = WindowHeight - y;

            x -= 50;
            y -= 50;


            x = x * (DataWidth / WindowWidth);
            y = y * (DataHeight / WindowHeight);

            x += DataOrigin.X;
            y += DataOrigin.Y;

            return new GLPoint(x, y);
        }

        public GLPoint ViewToScreen(GLPoint location) {
            var x = location.X;
            var y = location.Y;

            x -= DataOrigin.X;
            y -= DataOrigin.Y;

            x = x / (DataWidth / WindowWidth);
            y = y / (DataHeight / WindowHeight);

            x += 50;
            y += 50;

            y = WindowHeight - y;

            return new GLPoint(x, y);

        }
    }

    public interface ILineGraph {
        ObservableCollection<Line> Lines { get; }
        ObservableCollection<IDrawable> Markers { get; }

        void Draw();
        void Display(GLRect rect, bool draw);
        void Cleanup();

        bool TextEnabled { get; set; }

        Control Control { get; }

        GraphWindow Window { get; }

        bool PanningIsEnabled { get; set; }
        bool IsPanning { get; }

    }

    public class LineGraph : ILineGraph {
        readonly ObservableCollection<IDrawable> _markers = new ObservableCollection<IDrawable>();
        readonly ObservableCollection<Line> _lines = new ObservableCollection<Line>();
        readonly IDictionary<Line, DisplayList> _displayLists = new Dictionary<Line, DisplayList>();

        public bool IsPanning { get; private set; }

        bool _displayChanged = false;

        ITickBar _leftTickBar;
        ITickBar _bottomTickBar;
        int _xstart, _ystart;

        public GraphWindow Window { get; private set; }

        public ObservableCollection<IDrawable> Markers { get { return _markers; } }
        public ObservableCollection<Line> Lines { get { return _lines; } }

        public bool TextEnabled { get; set; }

        bool _panningIsEnabled;
        public bool PanningIsEnabled {
            get { return _panningIsEnabled; }
            set {
                _panningIsEnabled = value;
                if (!value) {
                    IsPanning = false;
                }
            }
        }

        GLControl _glcontrol;
        const string ZeroError = "WindowWidth cannot be zero, consider initializing LineGraph in the host's Loaded event";

        public LineGraph() {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;

            PanningIsEnabled = true;

            TextEnabled = true;
            InitializeUserControl();
            InitializeOpenGL();

            _lines.CollectionChanged += (s, args) => {
                if (args.Action == NotifyCollectionChangedAction.Reset) {
                    foreach (var d in _displayLists.Values) {
                        d.Dispose();
                    }
                    _displayLists.Clear();
                }
                if (args.OldItems != null) {
                    foreach (Line line in args.OldItems) {
                        RemoveLine(line);
                    }
                }
                if (args.NewItems != null) {
                    foreach (Line line in args.NewItems) {
                        AddLine(line);
                    }
                }
            };


            _markers.CollectionChanged += (s, args) => {
                if(args.Action == NotifyCollectionChangedAction.Reset) {
                    foreach(var m in _markers) {
                        m.Dispose();
                    }
                }
                if(args.OldItems != null) {
                    foreach(IDrawable m in args.OldItems) {
                        m.Dispose();
                    }
                }
            };

        }

        public void Cleanup() {
            foreach (var dl in _displayLists.Values) {
                dl.Dispose();
            }
            _leftTickBar.Dispose();
            _bottomTickBar.Dispose();
            _glcontrol.Dispose();
        }

        public void Draw() {
            if (Window == null) {
                Window = new GraphWindow {
                    DataOrigin = new GLPoint(0, 0),
                    DataHeight = 100,
                    DataWidth = 100,
                    WindowHeight = Control.Height,
                    WindowWidth = Control.Width
                };
            }
            if (Window.WindowWidth == 0 || Window.WindowHeight == 0) return;

            _glcontrol.MakeCurrent();

            if (_displayChanged) {
                _displayChanged = false;
                LoadDisplayLists();
            }


            GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.LoadIdentity();

            SetupProjection();

            ConfigureLeftTickBar();
            ConfigureBottomTickBar();

            _leftTickBar.DrawCrossLines();
            _bottomTickBar.DrawCrossLines();

            DrawDataAndMarkers();

            if (TextEnabled) {
                _leftTickBar.DrawTicks();
                _bottomTickBar.DrawTicks();
            }

            DrawDeadSpace();

            _glcontrol.SwapBuffers();
        }

        void DrawDataAndMarkers() {
            OpenGL.PushMatrix(() => {
                GL.Scale(1.0 / Window.WindowWidth, 1.0 / Window.WindowHeight, 1.0);
                GL.Translate(50, 50, 0);
                GL.Scale(Window.WindowWidth, Window.WindowHeight, 1.0);

                GL.Scale(1.0 / Window.DataWidth, 1.0 / Window.DataHeight, 0);
                GL.Translate(-Window.DataOrigin.X, -Window.DataOrigin.Y, 0);

                DrawData();
                DrawMarkers();
            });
        }

        void DrawDeadSpace() {
            OpenGL.PushMatrix(() => {
                GL.Scale(1.0 / Window.WindowWidth, 1.0 / Window.WindowHeight, 1.0);
                GL.Color3(1.0, 1.0, 1.0);
                OpenGL.Begin(BeginMode.Quads, () => {
                    GL.Vertex2(0, 50);
                    GL.Vertex2(50, 50);
                    GL.Vertex2(50, 0);
                    GL.Vertex2(0, 0);
                });
            });
        }

        public void StartPan(int xpos, int ypos) {
            IsPanning = true;
            _xstart = xpos;
            _ystart = ypos;
        }

        public void StopPan() {
            IsPanning = false;
        }

        public void Pan(int xpos, int ypos) {
            if (!IsPanning) return;
            var xoffset = -(((xpos - _xstart) / (Window.WindowWidth * 1.0)) * (Window.DataWidth));
            var yoffset = ((ypos - _ystart) / (Window.WindowHeight * 1.0)) * (Window.DataHeight);
            _xstart = xpos;
            _ystart = ypos;
            Window.DataOrigin = new GLPoint(Window.DataOrigin.X + xoffset, Window.DataOrigin.Y + yoffset);
            Draw();
        }

        public void Zoom(double zdelta) {
            var percentageWidth = ((Window.DataWidth) / 100.0) * 10;
            var percentageHeight = ((Window.DataHeight) / 100.0) * 10;

            if (zdelta > 0) {
                Window.DataWidth -= percentageWidth;
                Window.DataHeight -= percentageHeight;
                Window.DataOrigin = new GLPoint(Window.DataOrigin.X + percentageWidth / 2.0,
                                              Window.DataOrigin.Y + percentageHeight / 2.0);
            } else {
                Window.DataWidth += percentageWidth;
                Window.DataHeight += percentageHeight;
                Window.DataOrigin = new GLPoint(Window.DataOrigin.X - percentageWidth / 2.0,
                                                Window.DataOrigin.Y - percentageHeight / 2.0);
            }
            Draw();
        }

        public void Display(GLRect rect, bool draw) {
            if (Window == null) {
                Window = new GraphWindow();
            }

            var xyratio = Window.DataWidth / Window.DataHeight;
            var newRatio = rect.Width / rect.Height;

            Window.DataOrigin = rect.Location;
            Window.DataWidth = rect.Width;
            Window.DataHeight = rect.Height;
            Window.WindowWidth = _glcontrol.Width;
            Window.WindowHeight = _glcontrol.Height;

            if (Window.WindowWidth == 0) throw new Exception(ZeroError);
            if (Window.WindowHeight == 0) throw new Exception(ZeroError);

            if (Math.Abs(newRatio - xyratio) > 0.01) {
                _displayChanged = true;
            }
            if (draw) {
                Draw();
            }
        }

        public Control Control { get { return _glcontrol; } }

        void AddLine(Line line) {
            LoadDisplayList(line);
            line.Changed += LineChanged;
        }

        void RemoveLine(Line line) {
            var dl = _displayLists[line];
            dl.Dispose();
            _displayLists.Remove(line);
            line.Changed -= LineChanged;
        }

        void InitializeUserControl() {
            _glcontrol = new GLControl();
            _glcontrol.Resize += (s, args) => {
                SetWindowSize(_glcontrol.Width, _glcontrol.Height);
            };
            _glcontrol.MouseDown += (s, args) => {
                if (!PanningIsEnabled) return;
                StartPan(args.X, args.Y);
            };
            _glcontrol.MouseMove += (s, args) => {
                if (!PanningIsEnabled) return;
                Pan(args.X, args.Y);
            };
            _glcontrol.MouseUp += (s, args) => {
                if (!PanningIsEnabled) return;
                StopPan();
            };

            _glcontrol.MouseWheel += (s, args) => Zoom(args.Delta);
            _glcontrol.Paint += (s, args) => {
                Draw();
            };
        }

        void InitializeOpenGL() {
            _glcontrol.MakeCurrent();
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

            var aliasedLineWidthRange = new float[2];
            var antialiasedLineWidthRange = new float[2];
            float granularity;
            GL.GetFloat(GetPName.AliasedLineWidthRange, aliasedLineWidthRange);
            GL.GetFloat(GetPName.SmoothLineWidthRange, antialiasedLineWidthRange);
            GL.GetFloat(GetPName.SmoothLineWidthGranularity, out granularity);

            _leftTickBar = new VerticalTickBar();
            _bottomTickBar = new HorizontalTickBar();
        }

        void SetWindowSize(int windowWidth, int windowHeight) {
            if (Window != null) {
                Window.WindowWidth = windowWidth;
                Window.WindowHeight = windowHeight;
            }
            _glcontrol.MakeCurrent();
            GL.Viewport(0, 0, windowWidth, windowHeight);
            GL.Flush();
            Draw();
        }

        void LoadDisplayLists() {
            foreach (var dl in _displayLists.Values) {
                dl.Dispose();
            }
            _displayLists.Clear();

            foreach (var line in _lines) {
                LoadDisplayList(line);
            }
        }

        void LoadDisplayList(Line line) {
            _displayLists[line] = new DisplayList(() => {
                GL.LineWidth(line.Thickness);
                GL.Begin(BeginMode.LineStrip);
                var size = line.Points.Count;
                //GL.Color4(line.Color.R, line.Color.G,
                //          line.Color.B, line.Color.A);
                if (line.IsDynamic) {
                    var start = line.Points.FindIndex(0, p => p.X > Window.DataOrigin.X && p.X < (Window.DataOrigin.X + Window.DataWidth));
                    if (start != -1) {
                        for (var j = start; j < size; j++) {
                            var p = line.Points[j];
                            GL.Vertex2(p.X, p.Y);
                        }
                    }
                } else {
                    for (var j = 0; j < size; j++) {
                        var p = line.Points[j];
                        GL.Vertex2(p.X, p.Y);
                    }
                }
                GL.End();
                GL.LineWidth(1.0f);
            });
        }


        void ConfigureLeftTickBar() {
            _leftTickBar.Window = Window;
            _leftTickBar.MajorTick = 5;
            _leftTickBar.MinorTick = 1;
            _leftTickBar.TickStart = 0;

            while (Window.DataHeight / _leftTickBar.MajorTick > 20) {
                _leftTickBar.MinorTick = _leftTickBar.MajorTick;
                _leftTickBar.MajorTick *= 2;
            }

            _leftTickBar.RangeStart = Math.Floor(Window.Bottom);
            _leftTickBar.RangeStop = Math.Ceiling(Window.Top);
        }

        void ConfigureBottomTickBar() {
            _bottomTickBar.Window = Window;
            _bottomTickBar.MajorTick = 5;
            _bottomTickBar.MinorTick = 1;
            _bottomTickBar.TickStart = 0;

            while (Window.DataWidth / _bottomTickBar.MajorTick > 20) {
                _bottomTickBar.MinorTick = _bottomTickBar.MajorTick;
                _bottomTickBar.MajorTick *= 2;
            }


            _bottomTickBar.RangeStart = Math.Floor(Window.Start);
            _bottomTickBar.RangeStop = Math.Ceiling(Window.Finish);
        }

        void DrawMarkers() {
            foreach (var m in _markers) m.Draw(Window);
        }

        void DrawData() {
            foreach (var line in _displayLists.Keys) {
                var dl = _displayLists[line];
                line.Color.Draw();
                dl.Draw();
            }
        }

        void SetupProjection() {
            GL.LoadIdentity();
            GL.Ortho(0, 1, 0, 1, -1, 1);
        }

        void LineChanged(object sender, LineChangedEventArgs e) {
            var dl = _displayLists[e.Line];
            dl.Dispose();
            _displayLists.Remove(e.Line);
            LoadDisplayList(e.Line);
        }



    }

}
