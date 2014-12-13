﻿using UnityEngine;
using System;

namespace OSVR
{
	namespace Unity
	{
		/// <summary>
		/// Orientation interface: continually (or rather, when OSVR updates, in FixedUpdate) updates its orientation based on the incoming tracker data.
		/// </summary>
		public class OrientationInterface : MonoBehaviour {
			/// <summary>
			/// The interface path you want to connect to.
			/// </summary>
			public string path;

			/// <summary>
			/// This should be a reference to the single ClientKit instance in your project.
			/// </summary>
			public OSVRClientKit ClientKit;
			
			private OSVR.ClientKit.Interface iface;
			private OSVR.ClientKit.OrientationCallback cb;

			// Use this for initialization
			void Start () {
				iface = ClientKit.GetContext().getInterface (path);
				cb = new OSVR.ClientKit.OrientationCallback (callback);
				iface.registerCallback (cb, IntPtr.Zero);
			}
			
			private void callback(IntPtr userdata, ref OSVR.ClientKit.TimeValue timestamp, ref OSVR.ClientKit.OrientationReport report) {
				transform.rotation = Math.ConvertOrientation (report.rotation);
			}
		}
	}
}