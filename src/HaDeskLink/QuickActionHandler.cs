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
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Registers a global hotkey (Ctrl+Shift+H by default) to trigger Quick Actions.
/// </summary>
public class QuickActionHandler : IDisposable
{
    private readonly int _hotkeyId;
    private readonly Form _hiddenForm;
    private readonly Action _onHotkey;
    private bool _registered;
    private bool _disposed;

    public QuickActionHandler(Action onHotkey)
    {
        _onHotkey = onHotkey;
        _hotkeyId = 0xC000; // Custom hotkey ID

        // Create a hidden form to receive WM_HOTKEY messages
        _hiddenForm = new Form
        {
            Size = new System.Drawing.Size(0, 0),
            Opacity = 0,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None
        };
        _hiddenForm.Load += (s, e) => RegisterHotkey();
        _hiddenForm.Visible = false;

        // Intercept WM_HOTKEY
        _hiddenForm.GetType().GetProperty("Handle")?.GetValue(_hiddenForm); // Force handle creation
    }

    public void Start()
    {
        // Create handle without showing
        var handle = _hiddenForm.Handle;
        
        // Install a message filter to catch WM_HOTKEY
        Application.AddMessageFilter(new HotkeyMessageFilter(_hotkeyId, _onHotkey));
        
        RegisterHotkey();
    }

    private void RegisterHotkey()
    {
        if (_registered) return;

        // MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, KEY_H = 0x48
        _registered = RegisterHotKey(_hiddenForm.Handle, _hotkeyId, 0x2 | 0x4, 0x48);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_registered)
                UnregisterHotKey(_hiddenForm.Handle, _hotkeyId);
            _hiddenForm.Dispose();
            Application.RemoveMessageFilter(new HotkeyMessageFilter(_hotkeyId, _onHotkey));
            _disposed = true;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Message filter to catch WM_HOTKEY (0x0312) in the WinForms message loop.
    /// </summary>
    private class HotkeyMessageFilter : IMessageFilter
    {
        private readonly int _id;
        private readonly Action _callback;
        private DateTime _lastTrigger = DateTime.MinValue;

        public HotkeyMessageFilter(int id, Action callback)
        {
            _id = id;
            _callback = callback;
        }

        public bool PreFilterMessage(ref Message m)
        {
            // WM_HOTKEY = 0x0312
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == _id)
            {
                // Debounce: ignore if triggered within last 500ms
                if ((DateTime.Now - _lastTrigger).TotalMilliseconds < 500) return true;
                _lastTrigger = DateTime.Now;
                
                _callback.Invoke();
                return true; // Handled
            }
            return false;
        }
    }
}