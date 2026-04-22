// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Quick Actions popup window - shows HA entity toggle buttons.
/// Triggered by global hotkey (Ctrl+Shift+H).
/// </summary>
public class QuickActionWindow : Form
{
    private readonly List<QuickAction> _actions;
    private readonly HaApiClient _api;
    private static QuickActionWindow? _instance;

    public QuickActionWindow(List<QuickAction> actions, HaApiClient api)
    {
        _actions = actions;
        _api = api;

        Text = "HA DeskLink - Quick Actions";
        Size = new Size(300, Math.Max(120, 50 + actions.Count * 45));
        MinimumSize = new Size(250, 120);
        MaximumSize = new Size(400, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        ShowInTaskbar = false;

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        if (_actions.Count == 0)
        {
            var lbl = new Label
            {
                Text = Localization.Get("quickactions_empty"),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lbl);
            return;
        }

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(10),
            WrapContents = false
        };

        foreach (var action in _actions)
        {
            var btn = new Button
            {
                Text = action.Name,
                Width = 260,
                Height = 38,
                Tag = action
            };
            btn.Click += OnActionClicked;
            panel.Controls.Add(btn);
        }

        Controls.Add(panel);
    }

    private async void OnActionClicked(object? sender, EventArgs e)
    {
        var btn = (Button)sender!;
        var action = (QuickAction)btn.Tag!;
        btn.Enabled = false;
        btn.Text = "⏳ ...";

        try
        {
            await _api.ToggleEntityAsync(action.EntityId);
            btn.Text = $"✓ {action.Name}";
            btn.BackColor = Color.LightGreen;
        }
        catch
        {
            btn.Text = $"✗ {action.Name}";
            btn.BackColor = Color.LightCoral;
        }

        // Auto-close after 1.5s
        await System.Threading.Tasks.Task.Delay(1500);
        Close();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Close when clicking outside
        BeginInvoke(new Action(() => Close()));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    public static void ShowActions(List<QuickAction> actions, HaApiClient api)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Close();
        }
        _instance = new QuickActionWindow(actions, api);
        _instance.Show();
        _instance.Focus();
    }
}

public class QuickAction
{
    public string EntityId { get; set; } = "";
    public string Name { get; set; } = "";

    public QuickAction() { }

    public QuickAction(string entityId, string name)
    {
        EntityId = entityId;
        Name = name;
    }
}