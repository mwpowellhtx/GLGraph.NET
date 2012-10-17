using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using OpenTK.Graphics.OpenGL;

namespace GLGraph.NET {
    public class HorizontalTickBar : ITickBar {
        public double RangeStart { get; set; }
        public double TickStart { get; set; }
        public double RangeStop { get; set; }
        public double MinorTick { get; set; }
        public double MajorTick { get; set; }
        public GraphWindow Window { get; set; }

        readonly IDictionary<string, PieceOfText> _texts = new Dictionary<string, PieceOfText>();
        readonly Font _font = new Font("Arial", 10);

        public void DrawTicks() {
            DrawBackground();
            DrawMajorMinorTicks();
            DrawText();
        }

        public void DrawCrossLines() {
            OpenGL.PushMatrix(() => {
                MoveFiftyPixelsRight();
                GL.Scale(1.0 / Window.DataWidth, 1.0 / Window.WindowHeight, 1);
                GL.Translate(-Window.DataOrigin.X, 50, 0);

                GL.Color4(0.0, 0.0, 0.0, 0.25);
                OpenGL.WithoutSmoothing(() => {
                    OpenGL.Begin(BeginMode.Lines, () => {
                        var majorTicks = RangeHelper.FindTicks(MajorTick, RangeStart, RangeStop);
                        foreach (var tick in majorTicks) {
                            GL.Vertex2(TickStart + tick, 0);
                            GL.Vertex2(TickStart + tick, Window.WindowHeight);
                        }
                    });
                });
            });
        }

        public void Dispose() {
            foreach (var t in _texts.Values) {
                t.Dispose();
            }
            _texts.Clear();
            _font.Dispose();
        }

        void DrawText() {
            OpenGL.PushMatrix(() => {
                MoveFiftyPixelsRight();
                GL.Scale(1.0 / Window.WindowWidth, 1.0 / Window.WindowHeight, 1.0);
                var majorTicks = RangeHelper.FindTicks(MajorTick, RangeStart, RangeStop);
                foreach (var tick in majorTicks) {
                    var tickText = tick.ToString(CultureInfo.InvariantCulture);
                    PieceOfText pot;
                    if (_texts.ContainsKey(tickText)) {
                        pot = _texts[tickText];
                    } else {
                        pot = new PieceOfText(_font, tickText);
                        _texts[tickText] = pot;
                    }
                    pot.Draw(new GLPoint(((tick - Window.Start) / Window.DataWidth) * Window.WindowWidth - 5, 0), null, null, false);
                }
            });
        }

        void DrawMajorMinorTicks() {
            OpenGL.PushMatrix(() => {
                MoveFiftyPixelsRight();

                GL.Scale(1.0 / Window.DataWidth, 1.0 / Window.WindowHeight, 1);
                GL.Translate(-Window.DataOrigin.X, 0, 0);

                GL.Color3(0.0, 0.0, 0.0);
                OpenGL.WithoutSmoothing(() => {
                    OpenGL.Begin(BeginMode.Lines, () => {
                        var majorTicks = RangeHelper.FindTicks(MajorTick, RangeStart, RangeStop);
                        var minorTicks = RangeHelper.FindTicks(MinorTick, RangeStart, RangeStop);
                        foreach (var tick in majorTicks) {
                            DrawMajorTick(TickStart + tick);
                        }
                        foreach (var tick in minorTicks.Where(x => !minorTicks.Any(y => Math.Abs(x - y) < 0.0001))) {
                            DrawMinorTick(TickStart + tick);
                        }
                    });
                });
            });
        }

        void DrawBackground() {
            OpenGL.PushMatrix(() => {
                GL.Scale(1.0 / Window.WindowWidth, 1.0 / Window.WindowHeight, 1);

                GL.Color3(1.0, 1.0, 1.0);
                OpenGL.WithoutSmoothing(() => {
                    OpenGL.Begin(BeginMode.Quads, () => {
                        GL.Vertex2(0, 50);
                        GL.Vertex2(Window.WindowWidth, 50);
                        GL.Vertex2(Window.WindowWidth, 0);
                        GL.Vertex2(0, 0);
                    });

                    GL.Color3(0.0, 0.0, 0.0);
                    OpenGL.Begin(BeginMode.Lines, () => {
                        GL.Vertex2(50, 50);
                        GL.Vertex2(Window.WindowWidth, 50);
                    });
                });
            });
        }



        void DrawMajorTick(double x) {
            GL.Vertex2(x, 50);
            GL.Vertex2(x, 30);
        }

        void DrawMinorTick(double x) {
            GL.Vertex2(x, 50);
            GL.Vertex2(x, 40);
        }

        void MoveFiftyPixelsRight() {
            GL.Scale(1.0 / Window.WindowWidth, 1.0 / Window.WindowHeight, 1.0);
            GL.Translate(50, 0, 0);
            GL.Scale(Window.WindowWidth, Window.WindowHeight, 1.0);
        }

    }
}