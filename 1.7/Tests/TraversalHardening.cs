using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Xenomorphtype;

internal static class TraversalHardening
{
    private static int assertions;
    private static void Check(bool result, string name)
    {
        if (!result) throw new Exception(name);
        assertions++;
        Console.WriteLine("PASS " + name);
    }

    private static void Main(string[] args)
    {
        var budget = new TraversalRetryBudget<string>();
        Check(!budget.RecordFailure("A", 10), "first reacquisition allowed");
        Check(!budget.RecordFailure("B", 10), "alternate objective allowed");
        Check(!budget.RecordFailure("A", 10), "second equivalent failure allowed");
        Check(budget.RecordFailure("A", 10) && budget.IsBlocked("A", 10), "same-tick replacement jobs share budget");
        Check(!budget.IsBlocked("B", 10), "one failed objective does not block another");
        Check(!budget.IsBlocked("A", 130), "bounded retry delay expires");
        budget.Reset();
        Check(!budget.IsBlocked("A", 10), "successful traversal resets retry history");
        for (int i = 0; i < 30; i++) budget.RecordFailure(i.ToString(), 20);
        Check(budget.Count == 8, "history remains bounded");
        budget.IsBlocked("missing", 620);
        Check(budget.Count == 0, "old failure entries expire");
        var phases = new TraversalRetryBudget<(int origin, int cargo)>();
        for (int i = 0; i < 3; i++) phases.RecordFailure((1, 0), 20);
        Check(!phases.IsBlocked((2, 0), 20), "movement changes attempt identity");
        Check(!phases.IsBlocked((1, 42), 20), "pickup/delivery identity permits reacquisition");

        string mod = Path.GetFullPath(args[0]);
        string game = Path.GetFullPath(Path.Combine(mod, "../.."));
        string workshop = Path.GetFullPath(Path.Combine(game, "../../workshop/content/294100"));
        string[] directories = { game, Path.Combine(mod, "1.7/Assemblies"),
            Path.Combine(workshop, "2023507013/1.7/Assemblies"),
            Path.Combine(workshop, "2009463077/1.7/Assemblies"),
            Path.Combine(workshop, "2640710953/1.7/Assemblies") };
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = directories.Select(directory => Path.Combine(directory, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        };
        Assembly xmt = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(mod, "1.7/Assemblies/XMT.dll"));
        Assembly verse = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(game, "Assembly-CSharp.dll"));
        Type utility = xmt.GetType("Xenomorphtype.InfiltrationUtility", true);
        Type network = xmt.GetType("Xenomorphtype.InfiltrationNetworkComponent", true);
        object NewNetwork(string category, params object[] endpoints)
        {
            object value = Activator.CreateInstance(network, true);
            network.GetField("ProviderKey").SetValue(value, "geometric");
            network.GetField("CategoryKey").SetValue(value, category);
            foreach (string field in new[] { "Members", "Endpoints" })
                foreach (object endpoint in endpoints) ((IList)network.GetField(field).GetValue(value)).Add(endpoint);
            return value;
        }
        // Opaque identities only. No Map/Pawn/region simulation or engine startup.
        object first = RuntimeHelpers.GetUninitializedObject(verse.GetType("Verse.Building", true));
        object second = RuntimeHelpers.GetUninitializedObject(verse.GetType("Verse.Building", true));
        bool Same(object a, object b) => (bool)utility.GetMethod("SameNetwork", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { a, b });
        object left = NewNetwork("vents", first, second);
        object equivalent = NewNetwork("vents", second, first);
        network.GetField("ProviderIdentity").SetValue(left, new object());
        network.GetField("ProviderIdentity").SetValue(equivalent, new object());
        Check(Same(left, equivalent), "contiguous mixed network: order/provider replacement preserves topology");
        Check(!Same(left, NewNetwork("vents", first)), "independent vents/airgap: split changes topology");
        Check(!Same(left, NewNetwork("coolers", first, second)), "network categories remain distinct");
        IList Networks(params object[] values)
        {
            IList result = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(network));
            foreach (object value in values) result.Add(value);
            return result;
        }
        IList oldNetworks = Networks(left);
        IList replacements = Networks(equivalent);
        MethodInfo reuse = utility.GetMethod("ReuseEquivalentComponents", BindingFlags.Static | BindingFlags.NonPublic);
        Check((int)reuse.Invoke(null, new object[] { oldNetworks, replacements }) == 1 && ReferenceEquals(replacements[0], left),
            "equivalent provider rebuild retains published component identity");
        Check(oldNetworks.Count == 1 && ReferenceEquals(oldNetworks[0], left), "reconciliation preserves an enumerated snapshot");
        object splitA = NewNetwork("vents", first);
        object splitB = NewNetwork("vents", second);
        IList split = Networks(splitA, splitB);
        Check((int)reuse.Invoke(null, new object[] { oldNetworks, split }) == 0,
            "network split is not mistaken for equivalent endpoint union");

        Type cacheType = xmt.GetType("Xenomorphtype.InfiltrationTopologyCache", true);
        object cache = Activator.CreateInstance(cacheType, true);
        cacheType.GetField("Components").SetValue(cache, oldNetworks);
        cacheType.GetField("ComponentsDirty").SetValue(cache, false);
        cacheType.GetField("PortsDirty").SetValue(cache, false);
        cacheType.GetMethod("InvalidateComponents").Invoke(cache, null);
        Check(ReferenceEquals(cacheType.GetField("Components").GetValue(cache), oldNetworks) && oldNetworks.Count == 1 &&
            (int)cacheType.GetField("ComponentsRevision").GetValue(cache) == 0,
            "network notification preserves published snapshot and semantic revision");
        Check(!(bool)cacheType.GetField("PortsDirty").GetValue(cache), "network notification alone does not invalidate ports");
        cacheType.GetMethod("InvalidateRegions").Invoke(cache, null);
        Check((bool)cacheType.GetField("RegionBindingsDirty").GetValue(cache) &&
            (int)cacheType.GetField("PortsRevision").GetValue(cache) == 0,
            "region notification requests verification without changing port revision");

        Type port = xmt.GetType("Xenomorphtype.InfiltrationPort", true);
        object regionA = RuntimeHelpers.GetUninitializedObject(verse.GetType("Verse.Region", true));
        object regionB = RuntimeHelpers.GetUninitializedObject(verse.GetType("Verse.Region", true));
        Array Port(object endpoint, object region)
        {
            object value = Activator.CreateInstance(port, true);
            port.GetField("Endpoint").SetValue(value, endpoint);
            port.GetField("Region").SetValue(value, region);
            Array array = Array.CreateInstance(port, 1);
            array.SetValue(value, 0);
            return array;
        }
        bool SamePorts(Array a, Array b, bool regions) => (bool)utility.GetMethod("SamePorts", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { a, b, regions });
        Check(SamePorts(Port(first, regionA), Port(first, regionB), false), "region recreation preserves usable endpoint/cell topology");
        Check(!SamePorts(Port(first, regionA), Port(first, regionB), true), "region recreation requires derived binding refresh");
        Check(!SamePorts(Port(first, regionA), Port(second, regionA), false), "changed usable endpoint changes port topology");
        Console.WriteLine(assertions + " narrow assertions passed; in-game traversal remains required.");
    }
}
