# Munyu 🧪

> A sleek, edge-docked drag-and-drop temporary file & link shelf for Windows.

---

## 💡 About The Project

I created **Munyu** after seeing an inspiring post on X (formerly Twitter) about a temporary file storage shelf concept.

When working on Windows, we often need a quick, zero-friction space to temporarily drop files, links, or text snippets before dragging them into emails, browser uploads, or other folders. **Munyu** stays neatly docked along your screen border, follows your cursor with interactive animated eyes, and holds your items ready whenever you need them.

> ⚠️ **Notice**: Please note that this project is currently **under active development**. Features, UI components, and behaviors may be updated or refined over time.

---

## 🏷️ Name Origin

The name **Munyu** comes from the Japanese sound effect (onomatopoeia) *"munyu"* (むにゅ), which describes the soft, squishy, and flexible feel of a slime sticking and conforming smoothly to a surface. The app seamlessly docks and adapts along your screen perimeter.

---

## ✨ Key Features

- **Screen Edge Docking**: Seamlessly docks to any monitor edge (**Top**, **Bottom**, **Left**, **Right**) and slides along the screen perimeter.
- **Drag & Drop Shelf**: Drop files, folders, URLs, or text snippets directly onto the shelf.
- **Smart Item Binders**: Dropping multiple items at once automatically packs them into an expandable inline binder.
- **Responsive Grid Sizing**: Icons scale dynamically to fit within available space without overflowing.
- **Symmetrical Resizing**: 
  - `MouseWheel`: Symmetrically resize shelf length along the screen edge.
  - `Shift + MouseWheel`: Symmetrically resize shelf thickness.
- **Global Hotkey & Quick Toggle**: 
  - Press `Ctrl + Shift + M` anytime to instantly toggle visibility.
  - Re-running `Munyu.exe` toggles island visibility (Show / Hide) seamlessly without creating duplicate processes.
- **Interactive Character Eyes**: Background eyes track your mouse cursor in real-time across the desktop.

---

## 🚀 Quick Start

### Running the App
1. Download `Munyu.exe`.
2. Run `Munyu.exe` directly on Windows 10/11 (standalone single-file executable, no installer required).

### Controls & Shortcuts

| Action | Shortcut / Gesture |
| :--- | :--- |
| **Toggle Visibility** | `Ctrl + Shift + M` or Double-click `Munyu.exe` |
| **Hide Shelf** | `Esc` key |
| **Resize Length** | Scroll `MouseWheel` over shelf |
| **Resize Thickness** | `Shift` + Scroll `MouseWheel` over shelf |
| **Move / Slide Shelf** | Click and drag the handle bar at the edge |
| **Expand / Collapse Binder** | Double-click binder item or notch header |
| **Item Options** | Right-click item (`Unpack`, `Delete`) |

---

## 🛠️ Built With

- **C# / WPF** (.NET 8.0)
- **Win32 API Interop** (High-DPI multi-monitor work area handling & IPC EventWaitHandle)

---

## ⚠️ Disclaimer & Limitation of Liability

This software is provided **"as is"** and **"as available"**, without warranty of any kind, express or implied, including but not limited to warranties of merchantability, fitness for a particular purpose, or non-infringement. 

**Use of this software is entirely at your own risk.** By downloading or using this application, you agree to the following terms:

1. **No Liability for Data Loss**: The author is not responsible or liable for any data loss, file deletion, corruption, or unintended file modifications resulting from drag-and-drop operations, file validation checks, or system shortcuts.
2. **No Liability for System Issues**: The author shall not be held liable for any system instability, crashes, performance degradation, or software conflicts caused by global hotkeys (`Ctrl + Shift + M`), Win32 API calls, display DPI scaling, or multi-monitor handling.
3. **Third-Party Content**: The user is solely responsible for verifying the safety of any files, URLs, links, or text snippets dropped onto or launched through this software.
4. **No Guarantee of Support or Updates**: Features may be modified, broken, or discontinued at any time without prior notice.

In no event shall the author or copyright holders be liable for any claim, damages, or other liability arising from, out of, or in connection with the software or the use of this software.

---

## 📄 License

Distributed under the MIT License.
