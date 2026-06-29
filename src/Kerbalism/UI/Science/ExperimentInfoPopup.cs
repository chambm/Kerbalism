using KERBALISM.KsmGui;
using UnityEngine;

namespace KERBALISM
{
	// A small read-only popup showing an experiment's info text. Used for "included"
	// experiments (sub-experiments auto-collected by another experiment) which have no
	// controllable module on the vessel, so the full ExperimentPopup doesn't apply.
	public static class ExperimentInfoPopup
	{
		private static KsmGuiWindow window;

		public static void Show(ExperimentInfo expInfo)
		{
			if (expInfo == null)
				return;

			// only keep one info popup open at a time
			if (window != null)
			{
				window.Close();
				window = null;
			}

			window = new KsmGuiWindow(KsmGuiWindow.LayoutGroupType.Vertical, true, KsmGuiStyle.defaultWindowOpacity, true, 0, TextAnchor.UpperLeft, 5f);
			KsmGuiWindow thisWindow = window;
			window.OnClose = () => { if (window == thisWindow) window = null; };
			window.SetLayoutElement(false, false, 350, -1);

			KsmGuiHeader header = new KsmGuiHeader(window, expInfo.Title, default, 120);
			new KsmGuiIconButton(header, Textures.KsmGuiTexHeaderClose, () => thisWindow.Close(), Local.SCIENCEARCHIVE_closebutton);//"close"

			string info = string.IsNullOrEmpty(expInfo.ModuleInfo) ? expInfo.Title : expInfo.ModuleInfo;
			KsmGuiTextBox infoBox = new KsmGuiTextBox(window, info);
			infoBox.SetLayoutElement(false, true, 350);

			window.RebuildLayout();
		}
	}
}
