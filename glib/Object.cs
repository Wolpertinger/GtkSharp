// Object.cs - GObject class wrapper implementation
//
// Authors: Bob Smith <bob@thestuff.net>
//	    Mike Kestner <mkestner@speakeasy.net>
//
// (c) 2001 Bob Smith and Mike Kestner
//
// TODO:
//   Could remove `disposed' for a check if an object is on the dispose_queue_list.
//
namespace GLib {

	using System;
	using System.Collections;
	using System.ComponentModel;
	using System.Reflection;
	using System.Runtime.InteropServices;

	[AttributeUsage(AttributeTargets.All)]
	public class WrapperClassAttribute : Attribute {

		public WrapperClassAttribute () : base () {}
	}

	/// <summary>
	///	Object Class
	/// </summary>
	///
	/// <remarks>
	///	Wrapper class for GObject.
	/// </remarks>

	public class Object : IWrapper, IDisposable {

		// Private class and instance members
		IntPtr _obj;
		protected bool needs_ref = true;
		EventHandlerList _events;
		bool disposed = false;
		Hashtable Data;
		static Hashtable Objects = new Hashtable();
		static Queue PendingDestroys = new Queue ();
		static bool idle_queued;

		//
		// The destructor is invoked by a thread
		//
		~Object ()
		{
			Dispose ();
		}

		static bool PerformQueuedUnrefs ()
		{
			Object [] objects;

			lock (PendingDestroys){
				objects = new Object [PendingDestroys.Count];
				PendingDestroys.CopyTo (objects, 0);
				PendingDestroys.Clear ();
			}
			lock (typeof (Object))
				idle_queued = false;

			foreach (Object o in objects){
				if (o._obj == IntPtr.Zero)
					continue;
				
				o.Unref ();
				o._obj = IntPtr.Zero;
			}
			return false;
		}

		/// <summary>
		///	Dispose Method 
		/// </summary>
		///
		/// <remarks>
		///	Disposes of the raw object. Only override this if
		///	the Raw object should not be unref'd when the object
		///	is garbage collected.
		/// </remarks>

		public void Dispose ()
		{
			if (disposed)
				return;

			disposed = true;
			Objects.Remove (_obj);
			lock (PendingDestroys){
				PendingDestroys.Enqueue (this);
				lock (typeof (Object)){
					if (!idle_queued){
						Idle.Add (new IdleHandler (PerformQueuedUnrefs));
						idle_queued = true;
					}
				}
			}
			GC.SuppressFinalize (this);
		}

		[DllImport("libgobject-2.0-0.dll")]
		static extern void g_object_unref (IntPtr raw);
		
		[DllImport("libgobject-2.0-0.dll")]
		static extern void g_object_ref (IntPtr raw);

		/// <summary>
		///   Ref Method
		/// </summary>
		///
		/// <remarks>
		///   Increases the reference count on the native object.
		///   This method is used by generated classes and structs,
		///   and should not be used in user code.
		/// </remarks>
		public virtual void Ref ()
		{
			if (_obj == IntPtr.Zero)
				return;

			g_object_ref (_obj);
		}

		/// <summary>
		///   Unref Method
		/// </summary>
		///
		/// <remarks>
		///   Decreases the reference count on the native object.
		///   This method is used by generated classes and structs,
		///   and should not be used in user code.
		///
		///   This method should not be invoked by a thread.
		/// </remarks>
		public virtual void Unref ()
		{
			if (_obj == IntPtr.Zero)
				return;

			g_object_unref (_obj);
		}
		
		
		/// <summary>
		///	GetObject Shared Method 
		/// </summary>
		///
		/// <remarks>
		///	Used to obtain a CLI typed object associated with a 
		///	given raw object pointer. This method is primarily
		///	used to wrap object references that are returned 
		///	by either the signal system or raw class methods that
		///	return GObject references.
		/// </remarks>
		///
		/// <returns>
		///	The wrapper instance.
		/// </returns>

		public static Object GetObject(IntPtr o)
		{
			WeakReference obj = Objects[o] as WeakReference;
			if (obj != null) {
				// If the target object has not been collected, use it...
				if (obj.IsAlive)
					return obj.Target as GLib.Object;

				// ... otherwise we create a new wrapper around the IntPtr.
				Objects [o] = null;
			}

			return GtkSharp.ObjectManager.CreateObject(o); 
		}

		[DllImport("gtksharpglue")]
		static extern uint gtksharp_register_type (string name, uint parent_type);

		public static GLib.Type RegisterGType (System.Type t)
		{
			System.Type parent = t.BaseType;
			PropertyInfo pi = parent.GetProperty ("GType", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
			if (pi == null) {
				Console.WriteLine ("null PropertyInfo");
				return null;
			}
			uint parent_gtype = (uint) pi.GetValue (null, null);
			string name = t.Namespace.Replace(".", "_") + t.Name;
			GtkSharp.ObjectManager.RegisterType (name, t.Namespace + t.Name, t.Assembly.GetName().Name);
			return new GLib.Type (gtksharp_register_type (name, parent_gtype));
		}

		/// <summary>
		///	Object Constructor
		/// </summary>
		///
		/// <remarks>
		///	Dummy constructor needed for derived classes.
		/// </remarks>

		public Object () {
			needs_ref = false;
		}

		/// <summary>
		///	Object Constructor
		/// </summary>
		///
		/// <remarks>
		///	Creates an object from a raw object reference.
		/// </remarks>

		public Object (IntPtr raw)
		{
			Raw = raw;
		}

		[DllImport("libgobject-2.0-0.dll")]
		static extern IntPtr g_object_new (uint gtype, IntPtr dummy);

		public Object (GLib.Type gtype)
		{
			Raw = g_object_new (gtype.Value, IntPtr.Zero);
		}

		/// <summary>
		///	Raw Property
		/// </summary>
		///
		/// <remarks>
		///	The raw GObject reference associated with this wrapper.
		///	Only subclasses of Object can access this read/write
		///	property.  For public read-only access, use the
		///	Handle property.
		/// </remarks>

		protected virtual IntPtr Raw {
			get {
				return _obj;
			}
			set {
				if (needs_ref)
					g_object_ref (value);
				Objects [value] = new WeakReference (this);
				_obj = value;
			}
		}	

		/// <summary>
		///	GType Property
		/// </summary>
		///
		/// <remarks>
		///	The type associated with this object class.
		/// </remarks>

		[DllImport("gtksharpglue")]
		private static extern uint gtksharp_get_type_id (IntPtr obj);

		public static uint GType {
			get {
				return 0;
			}
		}

		/// <summary>
		///	GetGType Method
		/// </summary>
		///
		/// <remarks>
		///	Returns the GType of this object.
		/// </remarks>

		public uint GetGType () {
			if (_obj == IntPtr.Zero)
				return (uint) TypeFundamentals.TypeInvalid;
			else
				return gtksharp_get_type_id (_obj);
		}

		/// <summary>
		///	Handle Property
		/// </summary>
		///
		/// <remarks>
		///	The raw GObject reference associated with this object.
		///	Subclasses can use Raw property for read/write
		///	access.
		/// </remarks>

		public IntPtr Handle {
			get {
				return _obj;
			}
		}

		/// <summary>
		///	EventList Property
		/// </summary>
		///
		/// <remarks>
		///	A list object containing all the events for this 
		///	object indexed by the Gtk+ signal name.
		/// </remarks>

		protected EventHandlerList EventList {
			get {
				if (_events == null)
					_events = new EventHandlerList ();
				return _events;
			}
		}

		/// <summary>
		///	GetHashCode Method
		/// </summary>
		///
		/// <remarks>
		///	Calculates a hashing value.
		/// </remarks>

		public override int GetHashCode ()
		{
			return Handle.GetHashCode ();
		}

		/// <summary>
		///	GetData Method
		/// </summary>
		///
		/// <remarks>
		///	Accesses arbitrary data storage on the Object.
		/// </remarks>

		public object GetData (string key)
		{
			if (Data == null)
				return String.Empty;

			return Data [key];
		}

		/// <summary>
		///	SetData Method
		/// </summary>
		///
		/// <remarks>
		///	Stores arbitrary data on the Object.
		/// </remarks>

		public void SetData (string key, object val)
		{
			if (Data == null)
				Data = new Hashtable ();

			Data [key] = val;
		}

		[DllImport("libgobject-2.0-0.dll")]
		static extern void g_object_get_property (
				IntPtr obj, string name, IntPtr val);

		/// <summary>
		///	GetProperty Method
		/// </summary>
		///
		/// <remarks>
		///	Accesses a Value Property.
		/// </remarks>

		public void GetProperty (String name, GLib.Value val)
		{
			g_object_get_property (Raw, name, val.Handle);
		}

		[DllImport("libgobject-2.0-0.dll")]
		static extern void g_object_set_property (
				IntPtr obj, string name, IntPtr val);

		/// <summary>
		///	SetProperty Method
		/// </summary>
		///
		/// <remarks>
		///	Accesses a Value Property.
		/// </remarks>

		public void SetProperty (String name, GLib.Value val)
		{
			g_object_set_property (Raw, name, val.Handle);
		}

		[DllImport("gtksharpglue")]
		static extern bool gtksharp_is_object (IntPtr obj);

		internal static bool IsObject (IntPtr obj)
		{
			return gtksharp_is_object (obj);
		}

		[DllImport("gtksharpglue")]
		static extern int gtksharp_object_get_ref_count (IntPtr obj);

		public int RefCount {
			get {
				return gtksharp_object_get_ref_count (Handle);
			}
		}
	}
}
