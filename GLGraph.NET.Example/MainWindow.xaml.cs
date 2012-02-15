﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace GLGraph.NET.Example {
    public partial class MainWindow {
        public MainWindow() {
            InitializeComponent();
            if (DesignerProperties.GetIsInDesignMode(this)) return;


            Loaded += delegate {
                ShowDynamicGraph();
            };
        }

        void ShowStaticGraph() {
            graph.Lines.Add(new Line(1.0f, Colors.Black, new[] {
                new Point(0, 0),
                new Point(1, 1),
                new Point(2, 0),
                new Point(3, 1),
                new Point(4, 0),
            }));
            graph.Display(new Rect(0, -2, 10, 4), true);
        }

        DispatcherTimer _timer;
        void ShowDynamicGraph() {
            var line1 = new Line(1.0f, Colors.Red, new Point[] { });
            var line2 = new Line(1.0f, Colors.Green, new Point[] { });
            var line3 = new Line(1.0f, Colors.Blue, new Point[] { });
            graph.Lines.Add(line1);
            graph.Lines.Add(line2);
            graph.Lines.Add(line3);
            
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(0.001) };

            var i = 0;
            var rect = new Rect(0, -20, 500, 50);
            var rand = new Random();
            _timer.Tick += delegate {
                line1.AddPoint(new Point(i, (rand.Next() % 10) - 5));
                line2.AddPoint(new Point(i, (rand.Next() % 10) + 5));
                line3.AddPoint(new Point(i, (rand.Next() % 10) + 15));

                i++;

                if(i >= (rect.Width + rect.X)) {
                    var d = (i - (rect.Width + rect.X));
                    rect = new Rect(rect.X + d, rect.Y, rect.Width,rect.Height);
                    graph.Display(rect,false);
                }
                graph.Draw();
            };
            _timer.Start();
            graph.Display(rect, true);
        }
    }
}
