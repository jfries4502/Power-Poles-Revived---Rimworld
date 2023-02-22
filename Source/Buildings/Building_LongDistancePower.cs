using System;
using System.Collections.Generic;
using System.Diagnostics;
using RimForge.Comps;
using RimWorld;
using UnityEngine;
using Verse;
using Debug = UnityEngine.Debug;

namespace RimForge.Buildings
{
    public abstract class Building_LongDistancePower : Building, ICustomOverlayDrawer
    {
        private static int autoConnectFrame = -1;
        private static readonly List<Building_LongDistancePower> bin = new List<Building_LongDistancePower>(16);

        public abstract string Name { get; }
        public int TotalLinkCount => (connectedTo?.Count ?? 0) + (connectsToMe?.Count ?? 0);

        /// <summary>
        /// Gets the maximum link distance, in cells.
        /// Default value is float.PositiveInfinity.
        /// </summary>
        public virtual float MaxLinkDistance => float.PositiveInfinity;

        /// <summary>
        /// Gets the maximum number of LDC buildings that can be connected to this at once.
        /// Default value is int.MaxValue (around 2 billion).
        /// </summary>
        public virtual int MaxConnections => int.MaxValue;

        /// <summary>
        /// Is this LDC allowed to have any connections if it is under a roof?
        /// Default value is true. If false, and this building is under a roof or mountain roof,
        /// it will never be allowed to connect to anything.
        /// </summary>
        public virtual bool CanHaveConnectionsUnderRoof => true;

        /// <summary>
        /// Is this LDC allowed to form a connection if the target is under a roof?
        /// Default value is true.
        /// </summary>
        public virtual bool CanConnectedBeUnderRoof => true;

        /// <summary>
        /// Pretty self explainatory.
        /// </summary>
        public virtual bool DrawLinkRadiusWhenPlacing => true;

        public virtual bool IsUnderRoof => isUnderRoofCache;

        public string OverlayTexturePath => "RF/UI/NoUnderRoofIcon";

        public CompPowerTransmitter Power
        {
            get
            {
                return _power ??= GetComp<CompPowerTransmitter>();
            }
        }
        private CompPowerTransmitter _power;

        protected IReadOnlyCollection<Building_LongDistancePower> OwnedConnectionsSanitized
        {
            get
            {
                SanitizeLists();
                return connectedTo;
            }
        }

        private HashSet<Building_LongDistancePower> connectedTo = new HashSet<Building_LongDistancePower>(2);
        private HashSet<Building_LongDistancePower> connectsToMe = new HashSet<Building_LongDistancePower>(2);
        private bool isUnderRoofCache;

        public override void ExposeData()
        {
            base.ExposeData();

            bool isLoading = Scribe.mode != LoadSaveMode.Saving;

            SanitizeLists();

            if (isLoading)
                PreLinksReset();

            Scribe_Collections.Look(ref connectedTo, "ldp_connectedTo", LookMode.Reference);
            Scribe_Collections.Look(ref connectsToMe, "ldp_connectsToMe", LookMode.Reference);
            SanitizeLists();

            if (isLoading)
            {
                foreach (var item in connectedTo)
                    UponLinkAdded(item, true);
                foreach (var item in connectsToMe)
                    UponLinkAdded(item, false);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            base.Destroy(mode);

            SanitizeLists();
            DisconnectAll();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            Color red = Color.Lerp(Color.red, Color.white, 0.3f);
            Color green = Color.Lerp(Color.green, Color.white, 0.3f);
            Color blue = Color.Lerp(Color.blue, Color.white, 0.35f);

            bool multiSelected = Find.Selector.SelectedObjects.Count > 1;

            if (!multiSelected)
            {
                yield return new Command_Target()
                {
                    defaultLabel = "RF.LDP.LinkLabel".Translate(Name),
                    action = (t) =>
                    {
#if !V12
                        Thing thing = t.Thing;
#else
                        Thing thing = t;
#endif
                        // Try link to this.
                        bool worked = TryAddLink(thing as Building_LongDistancePower);
                        if (!worked)
                            Core.Warn("Failed to link!");

                    },
                    targetingParams = new TargetingParameters()
                    {
                        canTargetBuildings = true,
                        canTargetPawns = false,
                        canTargetSelf = false,
                        mapObjectTargetsMustBeAutoAttackable = false,
                        validator = info =>
                        {
                            if (!info.HasThing)
                                return false;
                            return info.Thing is Building_LongDistancePower thing && CanLinkTo(thing);
                        }
                    },
                    icon = Content.LinkIcon,
                    defaultIconColor = green
                };
            }

            if (TotalLinkCount > 0)
            {
                if (!multiSelected)
                {
                    IEnumerable<FloatMenuOption> GenOptions()
                    {
                        yield return new FloatMenuOption("RF.LDP.UnLinkAllLabel".Translate(), DisconnectAll);
                    }

                    yield return new Command_TargetWithDropdown()
                    {
                        defaultLabel = "RF.LDP.UnLinkLabel".Translate(Name),
                        action = (t) =>
                        {
#if !V12
                            Thing thing = t.Thing;
#else
                            Thing thing = t;
#endif
                            // Try un-link to this.
                            bool worked = TryRemoveLink(thing as Building_LongDistancePower);
                            if (!worked)
                                Core.Warn("Failed to un-link!");

                        },
                        targetingParams = new TargetingParameters()
                        {
                            canTargetBuildings = true,
                            canTargetPawns = false,
                            canTargetSelf = false,
                            mapObjectTargetsMustBeAutoAttackable = false,
                            validator = info =>
                            {
                                if (!info.HasThing)
                                    return false;
                                return info.Thing is Building_LongDistancePower thing && IsLinkedTo(thing);
                            }
                        },
                        icon = Content.LinkIcon,
                        defaultIconColor = red,
                        FloatMenuOptionsGenerator = GenOptions
                    };
                }
                else
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = "RF.LDP.UnLinkAllLabel".Translate(Name),
                        action = DisconnectAll,
                        icon = Content.LinkIcon,
                        defaultIconColor = red

                    };
                }
            }

            yield return new Command_Action()
            {
                defaultLabel = "RF.LDP.AutoLinkLabel".Translate(),
                defaultDesc = "RF.LDP.AutoLinkDesc".Translate(Name),
                action = () =>
                {
                    if (autoConnectFrame == Time.frameCount)
                        return;
                    autoConnectFrame = Time.frameCount;

                    List<Building_LongDistancePower> powers = new List<Building_LongDistancePower>();
                    foreach (var item in Find.Selector.SelectedObjectsListForReading)
                    {
                        if (item is Building_LongDistancePower pp)
                            powers.Add(pp);
                    }
                    if (powers.Count < 2)
                        return;

                    AutoConnectAll(powers);
                },
                icon = Content.LinkIcon,
                defaultIconColor = blue,
                disabled = !multiSelected,
                disabledReason = "RF.LDP.AutoLinkDisabled".Translate(Name)
            };
        }

        public override void Draw()
        {
            base.Draw();

            if (CanHaveConnectionsUnderRoof)
                return;

            if (isUnderRoofCache)
                this.DrawCustomOverlay();
        }

        public override void TickRare()
        {
            base.TickRare();

            isUnderRoofCache = Position.Roofed(Map);

            if (!CanHaveConnectionsUnderRoof && IsUnderRoof && TotalLinkCount > 0)
            {
                // Remove all connections!
                DisconnectAll();
                Messages.Message("RF.LDP.RemovedLinkRoofBuiltSelf".Translate(this.LabelCap), MessageTypeDefOf.CautionInput);
                return;
            }

            if (!CanConnectedBeUnderRoof)
            {
                foreach (var item in GetAllLinked(false))
                {
                    if (item.IsUnderRoof)
                        bin.Add(item);
                }

                foreach (var item in bin)
                {
                    if (TryRemoveLink(item))
                        Messages.Message("RF.LDP.RemovedLinkRoofBuilt".Translate(item.LabelCap), MessageTypeDefOf.CautionInput);
                }
                bin.Clear();
            }
        }

        

        public virtual void AutoConnectAll(List<Building_LongDistancePower> powers)
        {
            Core.Log($"Starting auto-link of {powers.Count} LDP buildings.");

            // Clear existing connections.
            foreach (var item in powers)
            {
                if (item.DestroyedOrNull())
                    continue;

                item.SanitizeLists();
                item.DisconnectAll();
            }

            (Building_LongDistancePower item, float dst) FindClosest(Building_LongDistancePower closeTo, List<Building_LongDistancePower> others)
            {
                float minDst = float.MaxValue;
                Building_LongDistancePower output = null;

                foreach (var item in others)
                {
                    if (item == closeTo)
                        continue;

                    if (!closeTo.CanLinkTo(item))
                        continue;

                    float dst = (item.Position - closeTo.Position).LengthHorizontalSquared;
                    if (dst < minDst)
                    {
                        minDst = dst;
                        output = item;
                    }
                }

                return (output, minDst);
            }

            int connected = 0;

            // Try to add all items to a single network.
            List<Building_LongDistancePower> open = new List<Building_LongDistancePower>();
            List<Building_LongDistancePower> grid = new List<Building_LongDistancePower>();
            List<(Building_LongDistancePower original, Building_LongDistancePower item, float dst)> temp = new List<(Building_LongDistancePower, Building_LongDistancePower, float)>();
            foreach (var item in powers)
            {
                if (!item.DestroyedOrNull())
                    open.Add(item);
            }

            // Choose a random pole for the first one to go on the grid. It would make more sense to choose a pole
            // closest to the center of mass... But picking a random one generally works fine.
            int starterIndex = Rand.Range(0, open.Count);
            var starter = open[starterIndex];
            open.RemoveAt(starterIndex);
            grid.Add(starter);

            // This algorithm can take quite a few iterations to make the network.
            // In some cases, it could go on forever. Using stopwatch to limit the max time and prevent an infinite loop.
            Stopwatch timer = new Stopwatch();
            timer.Start();

            while (open.Count > 0)
            {
                temp.Clear();
                foreach (var item in grid)
                {
                    // What to add:
                    // For every item in the grid, the closest 'open' item.
                    // Of every possibility, add the closest.
                    var pair = FindClosest(item, open);
                    if (pair.item != null)
                        temp.Add((item, pair.item, pair.dst));
                }

                if (temp.Count == 0)
                {
                    Core.Warn($"None of the {grid.Count} grid items can connect to any of the {open.Count} open items, breaking loop.");
                    break;
                }

                Building_LongDistancePower a = null, b = null;
                float smallest = float.MaxValue;
                foreach (var tupple in temp)
                {
                    if (tupple.dst < smallest)
                    {
                        smallest = tupple.dst;
                        a = tupple.original;
                        b = tupple.item;
                    }
                }

                if (a == null)
                {
                    Core.Warn("Failed to find a suitable connection between grid items and open items, breaking loop.");
                    break;
                }

                bool worked = a.TryAddLink(b);
                if (!worked)
                {
                    Core.Error("Bad shit: CanLinkTo returned true, but TryAddLink returned false in AutoConnect. Breaking autoconnect loop...");
                    break;
                }
                open.Remove(b);
                grid.Add(b);
                connected++;

            }

            timer.Stop();

            int failed = powers.Count - connected - 1; // There should be n - 1 connections between n poles.
            Core.Log($"Finished auto-connect of {connected + 1} in {timer.ElapsedMilliseconds} ms.");
            if (failed > 0)
                Core.Warn($"Failed to connect {failed} LDP buildings in auto-connect...");
        }

        public virtual void DisconnectAll()
        {
            int total = TotalLinkCount;
            if (total == 0)
                return;

            int i = 0;
            while (TotalLinkCount > 0)
            {
                var first = GetAllLinked(false).FirstOrFallback();
                if (first != null)
                    TryRemoveLink(first);
                i++;
                if (i > total + 1)
                {
                    Core.Error("Failed to unlink one or more connections in DisconnectAll().");
                    break;
                }
            }
        }

        public virtual bool CanLinkTo(Building_LongDistancePower other, bool checkOther = true)
        {
            if (other.DestroyedOrNull())
                return false;

            if (other == this)
                return false;

            if (connectedTo.Contains(other))
                return false;

            if (connectsToMe.Contains(other))
                return false;

            if (TotalLinkCount >= MaxConnections)
                return false;

            float dst = (Position - other.Position).LengthHorizontalSquared;
            if (dst > MaxLinkDistance * MaxLinkDistance)
                return false;

            if (!CanHaveConnectionsUnderRoof && IsUnderRoof)
                return false;

            if (!CanConnectedBeUnderRoof && other.IsUnderRoof)
                return false;

            if (checkOther && !other.CanLinkTo(this, false))
                return false;

            return true;
        }

        public bool IsLinkedTo(Building_LongDistancePower other)
        {
            if (other.DestroyedOrNull())
                return false;

            if (other == this)
                return false;

            return connectedTo.Contains(other) || connectsToMe.Contains(other);
        }

        /// <summary>
        /// Attempt to link this connection with another.
        /// Will return true if successful, false otherwise.
        /// A successful linking with result in the calling of <see cref="UponConnectionAdded(Building_LongDistancePower)"/>
        /// on both this connection and the other.
        /// </summary>
        /// <param name="item">The other connection to link to.</param>
        /// <returns>True upon success, false upon failure. Failure may indicate that they are already linked.</returns>
        public bool TryAddLink(Building_LongDistancePower item)
        {
            if (!CanLinkTo(item))
                return false;

            connectedTo.Add(item);
            bool check = item.connectsToMe.Add(this);
            if (!check)
                Debug.LogError("This is a problem. Not cool.");

            this.UponLinkAdded(item, true);
            item.UponLinkAdded(this, false);
            return true;
        }

        /// <summary>
        /// Attempt to remove a link between this connection and another.
        /// Will return true if successful, false otherwise.
        /// A successful un-linking with result in the calling of <see cref="UponConnectionRemoved(Building_LongDistancePower)"/>
        /// on both this connection and the other. 
        /// </summary>
        /// <param name="item">The other connection to un-link from.</param>
        /// <returns>True upon success, false upon failure. Failure may indicate that they are already un-linked.</returns>
        public bool TryRemoveLink(Building_LongDistancePower item)
        {
            if (item == null)
                return false;
            if (item == this)
                return false;

            if (connectedTo.Contains(item))
            {
                connectedTo.Remove(item);
                bool check = item.connectsToMe.Remove(this);
                if (!check)
                    Debug.LogError("Another problem. Damn.");

                this.UponLinkRemoved(item, true);
                item.UponLinkRemoved(this, false);
                return true;
            }

            if (connectsToMe.Contains(item))
            {
                return item.TryRemoveLink(this);
            }

            return false;
        }

        /// <summary>
        /// Called upon load. Indicates that any existing links are about be overwritten.
        /// Default implementation does nothing.
        /// </summary>
        protected virtual void PreLinksReset()
        {

        }

        /// <summary>
        /// Called whenever a new link is established, including upon load.
        /// This connection may be the 'owner' of the link. This is indicated by the <paramref name="isOwner"/> parameter.
        /// </summary>
        /// <param name="to">The connection that has been linked to. Will not be null or destroyed.</param>
        /// <param name="isOwner">Is this connection this owner of the link?</param>
        protected virtual void UponLinkAdded(Building_LongDistancePower to, bool isOwner)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
                return;

            var md = Map?.mapDrawer;
            if (md == null)
                return;

            md.MapMeshDirty(this.Position, MapMeshFlag.PowerGrid, true, false);
            foreach (var conn in GetAllLinked(false))
            {
                if (conn == null)
                    continue;
                md.SectionAt(conn.Position).dirtyFlags |= MapMeshFlag.PowerGrid;
            }

            Map.powerNetManager.Notfiy_TransmitterTransmitsPowerNowChanged(Power);
        }

        /// <summary>
        /// Called whenever an existing link is removed.
        /// This connection may have been the 'owner' of the link. This is indicated by the <paramref name="isOwner"/> parameter.
        /// </summary>
        /// <param name="to">The connection that has been un-linked. Will not be null, but may be destroyed.</param>
        /// <param name="isOwner">Was this connection the owner of the link?</param>
        protected virtual void UponLinkRemoved(Building_LongDistancePower from, bool isOwner)
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
                return;

            var md = Map?.mapDrawer;
            if (md == null)
                return;

            md.MapMeshDirty(this.Position, MapMeshFlag.PowerGrid, true, false);
            foreach (var conn in GetAllLinked(false))
            {
                if (conn == null)
                    continue;
                md.SectionAt(conn.Position).dirtyFlags |= MapMeshFlag.PowerGrid;
            }

            Map.powerNetManager.Notfiy_TransmitterTransmitsPowerNowChanged(Power);
        }

        /// <summary>
        /// Enumerates through all linked connections. If not sanitized, may include null or destroyed items.
        /// </summary>
        /// <param name="sanitize">If true, connections are checked to remove null or destroyed items.</param>
        /// <returns>An enumeration of all linked connections.</returns>
        public virtual IEnumerable<Building_LongDistancePower> GetAllLinked(bool sanitize)
        {
            if (sanitize)
                SanitizeLists();

            if (connectedTo != null)
                foreach (var item in connectedTo)
                    yield return item;

            if (connectsToMe != null)
                foreach (var item in connectsToMe)
                    yield return item;
        }

        private HashSet<Building_LongDistancePower> CreateList(int alloc = 2)
        {
            return new HashSet<Building_LongDistancePower>(alloc);
        }

        /// <summary>
        /// Ensures that the lists are not null, and clears out destroyed or
        /// removed connections from the lists.
        /// </summary>
        private void SanitizeLists()
        {
            connectedTo ??= CreateList();
            connectsToMe ??= CreateList();
            connectedTo.RemoveWhere(item => item.DestroyedOrNull());
            connectsToMe.RemoveWhere(item => item.DestroyedOrNull());
        }

        private class Command_TargetWithDropdown : Command_Target
        {
            public Func<IEnumerable<FloatMenuOption>> FloatMenuOptionsGenerator;

            public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
            {
                get
                {
                    if (FloatMenuOptionsGenerator == null)
                        yield break;

                    foreach (var item in FloatMenuOptionsGenerator.Invoke())
                        yield return item;
                }
            }
        }
    }
}
