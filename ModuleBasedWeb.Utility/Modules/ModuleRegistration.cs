namespace ModuleBasedWeb.Utility.Modules
{
	public static class ModuleRegistration
	{
		public static List<Registration> Modules { get; private set; }

		public static void AddModule(Registration module)
		{
			if (Modules is null) Modules = new List<Registration>();
			Modules.Add(module);
		}

		public static void ClearModules() => Modules = new List<Registration>();

		public static void AddModuleRole(string module, string role)
		{
			Registration mod = Modules.First(a => a.Name == module);
			if (mod.Roles is null) mod.Roles = new List<string>();
			mod.Roles.Add(role);
		}
	}
}
