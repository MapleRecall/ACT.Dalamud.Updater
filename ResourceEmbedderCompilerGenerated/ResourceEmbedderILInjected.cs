using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ResourceEmbedderCompilerGenerated;

[CompilerGenerated]
[ComVisible(true)]
public static class ResourceEmbedderILInjected
{
	private static Assembly FindMainAssembly(AssemblyName requestedAssemblyName)
	{
		if (requestedAssemblyName == null)
		{
			throw new ArgumentNullException("requestedAssemblyName");
		}
		if (!requestedAssemblyName.Name.EndsWith(".resources", StringComparison.InvariantCultureIgnoreCase))
		{
			throw new ArgumentException("Not a resource assembly");
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		string text = requestedAssemblyName.Name.Substring(0, requestedAssemblyName.Name.Length - ".resources".Length);
		Assembly[] array = assemblies;
		foreach (Assembly assembly in array)
		{
			if (assembly.GetName().Name == text)
			{
				return assembly;
			}
		}
		return null;
	}

	private static Assembly LoadFromResource(AssemblyName requestedAssemblyName, Assembly requestingAssembly)
	{
		if (requestedAssemblyName == null || requestedAssemblyName.CultureInfo == null)
		{
			return null;
		}
		while (true)
		{
			string arg = requestedAssemblyName.Name.Substring(0, requestedAssemblyName.Name.Length - ".resources".Length);
			string name = $"{arg}.{requestedAssemblyName.CultureInfo.Name}.resources.dll";
			Assembly assembly = requestingAssembly ?? FindMainAssembly(requestedAssemblyName);
			if (assembly == null)
			{
				return null;
			}
			using (Stream stream = assembly.GetManifestResourceStream(name))
			{
				if (stream != null)
				{
					byte[] array = new byte[stream.Length];
					stream.Read(array, 0, array.Length);
					return Assembly.Load(array);
				}
			}
			string name2 = requestedAssemblyName.CultureInfo.Parent.Name;
			if (string.IsNullOrEmpty(name2))
			{
				break;
			}
			requestedAssemblyName = new AssemblyName(requestedAssemblyName.FullName.Replace($"Culture={requestedAssemblyName.CultureInfo.Name}", $"Culture={name2}"));
		}
		return null;
	}

	private static bool IsLocalizedAssembly(AssemblyName requestedAssemblyName)
	{
		return requestedAssemblyName.Name.EndsWith(".resources", StringComparison.InvariantCultureIgnoreCase);
	}

	public static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
	{
		AssemblyName requestedAssemblyName;
		try
		{
			requestedAssemblyName = new AssemblyName(args.Name);
		}
		catch (Exception ex) when (ex is ArgumentException || ex is FileLoadException)
		{
			return null;
		}
		if (!IsLocalizedAssembly(requestedAssemblyName))
		{
			return null;
		}
		return LoadFromResource(requestedAssemblyName, args.RequestingAssembly);
	}

	public static void Attach()
	{
		AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
		{
			AssemblyName requestedAssemblyName;
			try
			{
				requestedAssemblyName = new AssemblyName(args.Name);
			}
			catch (Exception ex) when (ex is ArgumentException || ex is FileLoadException)
			{
				return null;
			}
			return (!IsLocalizedAssembly(requestedAssemblyName)) ? null : LoadFromResource(requestedAssemblyName, args.RequestingAssembly);
		};
	}
}
