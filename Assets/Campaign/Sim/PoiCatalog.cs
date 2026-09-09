using System.Collections.Generic;
using Century.Campaign.Model;
using UnityEngine;

namespace Century.Campaign.Sim
{
    /// <summary>What a choice does: resource swings, morale, healing, recruits, or a fight.</summary>
    public sealed class PoiOutcome
    {
        public string ResultText = string.Empty;

        public float Food;
        public int Coin, Denarii;
        public float Morale;      // delta applied to every living man, so it sticks past the hourly re-average
        public float Heal01;      // health restored to each wounded man
        public int Recruits;

        /// <summary>Recruit up to this many men in total (the looters' wagon frees however many are
        /// needed to reach the survivors' target). Zero means "use Recruits as written".</summary>
        public int RecruitsToReach;

        /// <summary>Condition of the men who join: massacre survivors crawl out at a fraction of
        /// this; the well-fed default is what a refugee column offers.</summary>
        public float RecruitHealthMin = 0.7f;
        public float RecruitHealthMax = 1f;
        public int RecruitExperienceMax;

        /// <summary>Loyalty the recruits arrive with. Deserters taken back into the standard come
        /// in well below the 0.5 default — men who broke once, watched by the relationship layer.</summary>
        public float RecruitLoyalty01 = 0.5f;

        public bool SpawnFight;
        public int FightStrength;

        /// <summary>Archetype for the spawned fighters ("germanic_looter" throws stones, not
        /// javelins); null keeps the raider default. And a name for the band, likewise.</summary>
        public string FightArchetype;
        public string FightName;

        /// <summary>The spawned warband carries the century's lost signum: beating it wins the
        /// standard back (army phase 3.5 — the speculator's lead closes phase 3's wound).</summary>
        public bool FightCarriesSignum;

        /// <summary>Officers judge the Centurion's choices (relationship layer): robbing a healer's
        /// grove sits ill with the medicus; burying the Roman dead sits well with the signifer.
        /// Post id (optio/signifer/tesserarius/medicus/speculator) → loyalty delta for its holder.</summary>
        public OfficerReaction[] Reactions;

        public struct OfficerReaction
        {
            public string Post;
            public float Loyalty;

            public OfficerReaction(string post, float loyalty)
            {
                Post = post;
                Loyalty = loyalty;
            }
        }

        /// <summary>Items granted into the party inventory: catalog id and count per entry.</summary>
        public ItemGrant[] Items;

        public struct ItemGrant
        {
            public string Id;
            public int Count;

            public ItemGrant(string id, int count)
            {
                Id = id;
                Count = count;
            }
        }

        /// <summary>Applies the outcome to the player's party and returns the line to report.</summary>
        public string Apply(CampaignState state, PartyState player, CampaignEventLog log, Vector3 position)
        {
            Stores s = player.Stores;
            s.Food = Mathf.Max(0f, s.Food + Food);
            s.Coin = Mathf.Max(0, s.Coin + Coin);
            s.Denarii = Mathf.Max(0, s.Denarii + Denarii);

            if (Morale != 0f || Heal01 > 0f)
            {
                List<SoldierRecord> men = player.Roster.Soldiers;
                for (int i = 0; i < men.Count; i++)
                {
                    SoldierRecord man = men[i];
                    if (!man.IsAlive) continue;
                    if (Morale != 0f) man.Morale01 = Mathf.Clamp01(man.Morale01 + Morale);
                    if (Heal01 > 0f && man.Health01 < 0.5f) man.Health01 = Mathf.Clamp01(man.Health01 + Heal01);
                }
                player.Morale.Value01 = player.Roster.AverageMorale01;
            }

            int recruits = Recruits;
            if (RecruitsToReach > 0)
                recruits = Mathf.Max(recruits, RecruitsToReach - player.Roster.ActiveCount);
            for (int i = 0; i < recruits; i++) player.Roster.Add(MakeRecruit(state));

            if (Items != null)
                for (int i = 0; i < Items.Length; i++)
                    player.Inventory.Add(Items[i].Id, Items[i].Count);

            if (SpawnFight)
            {
                PartyState raiders = PoiFightFactory.SpawnRaiders(
                    state, position, FightStrength, FightArchetype, FightName);
                if (FightCarriesSignum)
                {
                    raiders.CarriesPlayerSignum = true;
                    raiders.DisplayName = "The Signum's Captors";
                }
            }

            // The officers mark what kind of commander makes this kind of choice.
            if (Reactions != null)
            {
                for (int i = 0; i < Reactions.Length; i++)
                {
                    SoldierRecord officer = state.Posts.HolderOf(Reactions[i].Post, player.Roster);
                    if (officer == null) continue;

                    RelationshipLedger.AdjustLoyalty(officer, Reactions[i].Loyalty);

                    if (Mathf.Abs(Reactions[i].Loyalty) >= 0.05f)
                        log?.Push(
                            Reactions[i].Loyalty > 0 ? CampaignEventKind.Gain : CampaignEventKind.Loss,
                            Reactions[i].Loyalty > 0
                                ? $"{officer.DisplayName} approves"
                                : $"{officer.DisplayName} is displeased",
                            $"The {PostRoster.DisplayName(Reactions[i].Post)} marks the Centurion's choice",
                            state.Clock.Now.DayNumber);
                }
            }

            log?.Push(SpawnFight ? CampaignEventKind.Threat : CampaignEventKind.Gain,
                ResultText, string.Empty, state.Clock.Now.DayNumber);

            return ResultText;
        }

        private static readonly string[] Praenomina =
            { "Gaius", "Lucius", "Marcus", "Publius", "Quintus", "Titus", "Aulus", "Decimus" };
        private static readonly string[] Nomina =
            { "Valerius", "Aquilius", "Vorenus", "Secundus", "Felix", "Cornelius", "Fabius", "Sergius" };

        private SoldierRecord MakeRecruit(CampaignState state)
        {
            string name = $"{Praenomina[Random.Range(0, Praenomina.Length)][0]}. {Nomina[Random.Range(0, Nomina.Length)]}";
            return new SoldierRecord
            {
                Id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                DisplayName = name,
                RankId = "legionary",
                ArchetypeId = "legionary_heavy",
                Health01 = Random.Range(RecruitHealthMin, RecruitHealthMax),
                Stamina01 = Random.Range(0.5f, 0.9f),
                Morale01 = Random.Range(0.45f, 0.65f),
                Loyalty01 = RecruitLoyalty01,
                Experience = RecruitExperienceMax > 0 ? Random.Range(0, RecruitExperienceMax + 1) : 0,
                DayJoined = state != null ? state.Clock.Now.DayNumber : 0
            };
        }
    }

    public sealed class PoiChoice
    {
        public string Label;
        public string Hint;
        public PoiOutcome Outcome;

        public PoiChoice(string label, string hint, PoiOutcome outcome)
        {
            Label = label;
            Hint = hint;
            Outcome = outcome;
        }
    }

    public sealed class PoiEvent
    {
        public string Id;
        public string Title;
        public string Subtitle;
        public string Body;
        public string OfficerRemark;
        public PoiChoice[] Choices;
    }

    /// <summary>
    /// The authored library of overmap events, keyed by <see cref="PointOfInterest.EventId"/>. Plain
    /// C# so a designer needs no asset pipeline to add or tune one; it moves to ScriptableObjects when
    /// the content outgrows a single file.
    /// </summary>
    public static class PoiCatalog
    {
        private static readonly Dictionary<string, PoiEvent> Events = Build();

        public static PoiEvent Find(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            return Events.TryGetValue(eventId, out PoiEvent found) ? found : null;
        }

        /// <summary>
        /// Seeds a fresh Opportunity POI near a position — used by the Speculator's scouting and any
        /// event that reveals a prize. Discovered on creation so its marker shows at once. WHAT he
        /// found is rolled through the <see cref="SpeculatorInfluence"/> seam: the office's quality
        /// decides how often the report is wrong and how rich the real finds run. A false lead
        /// wears the same name as a real prize — the map must not spoil what the scout cannot know.
        /// </summary>
        public static PointOfInterest CreateOpportunity(CampaignState state, CampaignSettings settings, Vector3 near)
        {
            OpportunityTier tier = SpeculatorInfluence.Current.RollFind(state);

            string eventId;
            string displayName = "A scouted prize";
            switch (tier)
            {
                case OpportunityTier.FalseLead: eventId = "false_lead"; break;
                case OpportunityTier.Meagre: eventId = "opportunity_meagre"; break;
                case OpportunityTier.Rich: eventId = "opportunity_rich"; break;
                case OpportunityTier.Signum:
                    eventId = "signum_held";
                    displayName = "Word of the signum";
                    break;
                default: eventId = "opportunity"; break;
            }

            Vector2 dir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(120f, 220f);
            Vector3 pos = settings.ClampToWorld(near + new Vector3(dir.x, 0f, dir.y) * distance);

            var poi = new PointOfInterest
            {
                Id = state.MintId("poi"),
                Kind = PoiKind.Opportunity,
                DisplayName = displayName,
                EventId = eventId,
                WorldPosition = pos,
                Discovered = true
            };
            state.PointsOfInterest.Add(poi);
            return poi;
        }

        private static Dictionary<string, PoiEvent> Build()
        {
            var map = new Dictionary<string, PoiEvent>();

            void Add(PoiEvent e) => map[e.Id] = e;

            Add(new PoiEvent
            {
                Id = "shrine",
                Title = "A Roadside Shrine",
                Subtitle = "An altar to Mercury at the crossroads",
                Body = "A weathered altar stands where two tracks meet, its offering-bowl long empty. " +
                       "Travellers once paid the god for safe passage. You could do the same — or take what little remains.",
                OfficerRemark = "Optio Aquilius: \"The men could do with a blessing, sir. Gods know we've earned no luck.\"",
                Choices = new[]
                {
                    new PoiChoice("Leave an offering", "-20 denarii, the men take heart",
                        new PoiOutcome
                        {
                            Denarii = -20, Morale = 0.06f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Optio, 0.04f) },
                            ResultText = "You leave coin at the altar; the column marches easier."
                        }),
                    new PoiChoice("Take what coin remains", "+18 coin, some mutter of ill luck",
                        new PoiOutcome
                        {
                            Coin = 18, Morale = -0.04f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Optio, -0.05f) },
                            ResultText = "You pocket the offerings. A few men make warding signs."
                        }),
                    new PoiChoice("March on", "leave the shrine untouched",
                        new PoiOutcome { ResultText = "You leave the shrine as you found it." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "grove",
                Title = "A Druid's Grove",
                Subtitle = "Votive stones and herb-smoke in the pines",
                Body = "Smoke curls through the trees, and an old healer tends bundles of herbs among moss-grown " +
                       "stones. She watches your column without fear.",
                OfficerRemark = "Medicus Secundus: \"Those herbs, sir — worth more than gold to the wounded.\"",
                Choices = new[]
                {
                    new PoiChoice("Trade for medicine", "-40 coin, herbs and salves",
                        new PoiOutcome
                        {
                            Coin = -40,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("healing_herbs", 3),
                                new PoiOutcome.ItemGrant("healing_salve", 1),
                            },
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Medicus, 0.06f) },
                            ResultText = "You barter for her herbs; the medicus is well pleased."
                        }),
                    new PoiChoice("Ask her to tend the wounded", "heals the wounded, +morale",
                        new PoiOutcome { Heal01 = 0.2f, Morale = 0.04f, ResultText = "The healer works through the column; the worst hurts ease." }),
                    new PoiChoice("Rob the grove", "+70 coin, the men are shamed",
                        new PoiOutcome
                        {
                            Coin = 70, Morale = -0.09f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Medicus, -0.08f) },
                            ResultText = "You strip the grove bare. It sits ill with the men."
                        }),
                }
            });

            Add(new PoiEvent
            {
                Id = "refugees",
                Title = "A Column of Refugees",
                Subtitle = "Frightened folk on the old road",
                Body = "Roman and native both, fleeing the same war you are. Some are strong enough to carry a shield. " +
                       "All of them are hungry.",
                OfficerRemark = "Tesserarius Vorenus: \"Mouths to feed, sir. Or blades for the line. Your call.\"",
                Choices = new[]
                {
                    new PoiChoice("Take able men into the ranks", "+2 recruits, -30 food",
                        new PoiOutcome
                        {
                            Recruits = 2, Food = -30,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Optio, 0.04f) },
                            ResultText = "Two able men fall in with the column."
                        }),
                    new PoiChoice("Share your rations", "-40 food, the men's spirits rise",
                        new PoiOutcome { Food = -40, Morale = 0.07f, ResultText = "You feed the refugees. Word of Roman mercy spreads." }),
                    new PoiChoice("Turn them away", "costs nothing, costs something",
                        new PoiOutcome
                        {
                            Morale = -0.05f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Medicus, -0.05f) },
                            ResultText = "You wave them off the road. The men march in silence."
                        }),
                }
            });

            Add(new PoiEvent
            {
                Id = "merchant",
                Title = "A Travelling Merchant",
                Subtitle = "A Syrian trader with a laden mule-train",
                Body = "Even here, at the edge of the world, there is coin to be made. The trader spreads his hands " +
                       "and smiles a merchant's smile.",
                Choices = new[]
                {
                    new PoiChoice("Buy materials", "-60 denarii, timber, leather and rope",
                        new PoiOutcome
                        {
                            Denarii = -60,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("timber", 4),
                                new PoiOutcome.ItemGrant("leather_hides", 3),
                                new PoiOutcome.ItemGrant("rope_coils", 2),
                            },
                            ResultText = "You lay in materials for the road ahead."
                        }),
                    new PoiChoice("Buy food", "-50 denarii, +100 rations and flour",
                        new PoiOutcome
                        {
                            Denarii = -50, Food = 100,
                            Items = new[] { new PoiOutcome.ItemGrant("flour_sack", 2) },
                            ResultText = "The wagons are heavier with grain and good flour now."
                        }),
                    new PoiChoice("Change coin for denarii", "-100 coin, +40 denarii",
                        new PoiOutcome { Coin = -100, Denarii = 40, ResultText = "You trade local tokens for good Roman silver." }),
                    new PoiChoice("Move on", "keep your purse shut",
                        new PoiOutcome { ResultText = "You leave the trader to the road." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "watchtower",
                Title = "An Abandoned Watchtower",
                Subtitle = "A Roman tower, its garrison fled",
                Body = "The signal-tower stands empty on the ridge, commanding the valley. Kit may still lie within — " +
                       "but something has made a den of the lower room, and it is not sleeping quietly.",
                OfficerRemark = "Optio Aquilius: \"Could be arms up there, sir. Could be a bear. Only one way to know.\"",
                Choices = new[]
                {
                    new PoiChoice("Search it", "salvage, +30 denarii — but rouse what's inside",
                        new PoiOutcome
                        {
                            Denarii = 30, SpawnFight = true, FightStrength = 8,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("map_marsh_paths", 1),
                                new PoiOutcome.ItemGrant("mead_jar", 2),
                                new PoiOutcome.ItemGrant("timber", 3),
                                new PoiOutcome.ItemGrant("rope_coils", 2),
                            },
                            ResultText = "You force the door. Something comes out fighting."
                        }),
                    new PoiChoice("Camp in the tower", "+8 firewood bundles, +morale",
                        new PoiOutcome
                        {
                            Morale = 0.04f,
                            Items = new[] { new PoiOutcome.ItemGrant("firewood_bundle", 8) },
                            ResultText = "You take the upper floor for the night. Dry walls, for once."
                        }),
                    new PoiChoice("Leave it be", "walk away",
                        new PoiOutcome { ResultText = "You leave the tower to its tenant." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "raiders",
                Title = "A Raider Camp",
                Subtitle = "Germanic warriors, drunk on plunder",
                Body = "They sprawl around their fires, Roman spoils piled beside them — cloaks, standards, a legion's " +
                       "shame. They have not seen you yet.",
                OfficerRemark = "Tesserarius Vorenus: \"Catch them like this and they'll not stand long, sir.\"",
                Choices = new[]
                {
                    new PoiChoice("Attack them", "a fight — but the spoils are Roman",
                        new PoiOutcome { SpawnFight = true, FightStrength = 16, ResultText = "You form up and fall on the camp." }),
                    new PoiChoice("Slip past", "avoid the fight, lose a little face",
                        new PoiOutcome { Morale = -0.03f, ResultText = "You give the camp a wide berth. Some men wanted the fight." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "battlefield",
                Title = "An Old Battlefield",
                Subtitle = "Bones and broken shields in the grass",
                Body = "A legion died here, or a warband — the crows no longer care which. Good iron rusts in the mud, " +
                       "and the Roman dead lie unburied.",
                OfficerRemark = "Signifer Felix: \"Our own may lie here, sir. We should not pass them by.\"",
                Choices = new[]
                {
                    new PoiChoice("Scavenge the field", "salvage and iron — the work sits ill",
                        new PoiOutcome
                        {
                            Morale = -0.05f,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("scutum_spare", 2),
                                new PoiOutcome.ItemGrant("helmet_spare", 2),
                                new PoiOutcome.ItemGrant("iron_ingots", 3),
                                new PoiOutcome.ItemGrant("linen_bandages", 2),
                                new PoiOutcome.ItemGrant("orders_varus", 1),
                            },
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Signifer, -0.06f) },
                            ResultText = "You strip the field of anything useful."
                        }),
                    new PoiChoice("Bury the dead", "no spoils, but the men stand taller",
                        new PoiOutcome
                        {
                            Morale = 0.08f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Signifer, 0.08f) },
                            ResultText = "You give the dead the rites of Rome. The men are the prouder for it."
                        }),
                    new PoiChoice("March on", "leave the field to the crows",
                        new PoiOutcome { ResultText = "You leave the dead to the grass." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "raider_warcamp",
                Title = "A Raider War-Camp",
                Subtitle = "A palisaded muster, banners on the stakes",
                Body = "Not a band drunk on plunder — a muster. A rough palisade, sentries who walk their rounds, " +
                       "and captured Roman kit stacked like firewood. Breaking a camp like this would be felt " +
                       "across the whole district. So would failing to.",
                OfficerRemark = "Speculator: \"Two dozen spears at least, sir, and they keep a proper watch. This one would cost us.\"",
                Choices = new[]
                {
                    new PoiChoice("Storm the palisade", "a hard fight — a war-camp's worth of spoils",
                        new PoiOutcome
                        {
                            Denarii = 120, SpawnFight = true, FightStrength = 26,
                            FightName = "War-Camp Muster",
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("pila_bundle", 2),
                                new PoiOutcome.ItemGrant("scutum_spare", 2),
                                new PoiOutcome.ItemGrant("iron_ingots", 4),
                                new PoiOutcome.ItemGrant("fur_pelts", 3),
                                new PoiOutcome.ItemGrant("mead_jar", 3),
                            },
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Signifer, 0.05f) },
                            ResultText = "The century goes over the stakes with the dawn."
                        }),
                    new PoiChoice("Slip past in the dark", "no fight; the muster stands",
                        new PoiOutcome { Morale = -0.03f, ResultText = "You thread the column past their fires. The camp stands at your back." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "deserters",
                Title = "A Broken Tower",
                Subtitle = "Roman voices behind a barred door",
                Body = "Legionaries — a dozen days' beards, eyes that will not meet the crest. Deserters from the " +
                       "massacre, holed up with what they carried off. They are done running, if you will have them; " +
                       "men who broke once, carrying it with them.",
                OfficerRemark = "Optio Aquilius: \"Runners, sir. Take them if we must — but the standard remembers who left it.\"",
                Choices = new[]
                {
                    new PoiChoice("Take them back into the standard", "+3 seasoned recruits of doubtful loyalty",
                        new PoiOutcome
                        {
                            Recruits = 3, RecruitExperienceMax = 400, RecruitLoyalty01 = 0.25f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Optio, -0.05f) },
                            ResultText = "Three men fall in without a word. The ranks make room, slowly."
                        }),
                    new PoiChoice("Hang the ringleader, enlist the rest", "+2 recruits, discipline upheld",
                        new PoiOutcome
                        {
                            Recruits = 2, RecruitExperienceMax = 400, RecruitLoyalty01 = 0.4f, Morale = -0.03f,
                            Reactions = new[]
                            {
                                new PoiOutcome.OfficerReaction(PostId.Optio, 0.06f),
                                new PoiOutcome.OfficerReaction(PostId.Medicus, -0.05f),
                            },
                            ResultText = "The rope does its work. Two men take the oath again over the grave."
                        }),
                    new PoiChoice("Leave them to their tower", "the century keeps its own counsel",
                        new PoiOutcome { ResultText = "You leave them the tower and the shame of it." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "hunters_camp",
                Title = "A Hunters' Camp",
                Subtitle = "Chatti hunters, wary but not hostile",
                Body = "Racks of drying meat, stretched hides, and hunters who set down their bows slowly when they " +
                       "see the eagles. They have no love for Rome — but no appetite for dying over venison either. " +
                       "They will trade.",
                OfficerRemark = "Tesserarius Vorenus: \"Meat that doesn't march out of our stores, sir. I'd call that worth silver.\"",
                Choices = new[]
                {
                    new PoiChoice("Trade for meat and pelts", "-35 denarii, +70 food and hides",
                        new PoiOutcome
                        {
                            Denarii = -35, Food = 70,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("raw_game", 3),
                                new PoiOutcome.ItemGrant("fur_pelts", 2),
                            },
                            ResultText = "Silver for venison: both sides leave satisfied."
                        }),
                    new PoiChoice("Take it all", "a small fight; the district hears of it",
                        new PoiOutcome
                        {
                            Food = 90, SpawnFight = true, FightStrength = 8, FightName = "Chatti Hunters",
                            Items = new[] { new PoiOutcome.ItemGrant("fur_pelts", 4), new PoiOutcome.ItemGrant("raw_game", 4) },
                            Morale = -0.04f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Medicus, -0.06f) },
                            ResultText = "You take the camp. The hunters reach for their bows."
                        }),
                    new PoiChoice("March on", "leave them to the forest",
                        new PoiOutcome { ResultText = "You leave the hunters to their racks and fires." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "wrecked_convoy",
                Title = "A Wrecked Convoy",
                Subtitle = "Roman wagons overturned in a defile",
                Body = "A supply train died here: wagons on their sides, mules stripped to bone, grain sacks split " +
                       "across the track. Figures pick through the wreck — looters, stooped under sacks already. " +
                       "The kit they are carrying off is Roman.",
                OfficerRemark = "Valerius: \"Corpse-pickers, sir — stones and knives, no stomach for a line of shields.\"",
                Choices = new[]
                {
                    new PoiChoice("Drive them off the wagons", "a fight with looters; the convoy's stores",
                        new PoiOutcome
                        {
                            Food = 60, SpawnFight = true, FightStrength = 10,
                            FightArchetype = "germanic_looter", FightName = "Convoy Looters",
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("grain_sack", 3),
                                new PoiOutcome.ItemGrant("pila_bundle", 2),
                                new PoiOutcome.ItemGrant("linen_bandages", 2),
                                new PoiOutcome.ItemGrant("scutum_spare", 1),
                            },
                            ResultText = "Shields forward down the defile: the looters drop their sacks and stand."
                        }),
                    new PoiChoice("Take what's nearest and go", "+40 food, no fight — the rest is theirs",
                        new PoiOutcome
                        {
                            Food = 40, Morale = -0.02f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Signifer, -0.04f) },
                            ResultText = "You shoulder what's nearest and leave Roman kit to the carrion trade."
                        }),
                }
            });

            Add(new PoiEvent
            {
                Id = "slave_pens",
                Title = "A Slave Fair",
                Subtitle = "Romans in chains, buyers on the road",
                Body = "Pens of lashed hurdles, and in them Romans — soldiers, mule-drivers, a clerk still in his " +
                       "stained tunic — waiting to be sold when the buyers come. The looter bands bring their wagons " +
                       "here. The guards are many, but they are guards of chattel, not a line of spears.",
                OfficerRemark = "Valerius: \"This is where the wagons were headed, sir. Every one of them.\"",
                Choices = new[]
                {
                    new PoiChoice("Break the fair", "a fight with the guards; the pens come open",
                        new PoiOutcome
                        {
                            SpawnFight = true, FightStrength = 14,
                            FightArchetype = "germanic_looter", FightName = "Slave-Fair Guards",
                            Recruits = 3, RecruitHealthMin = 0.2f, RecruitHealthMax = 0.35f, RecruitExperienceMax = 250,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Signifer, 0.06f) },
                            ResultText = "The century comes out of the treeline in line. The pens come open."
                        }),
                    new PoiChoice("Ransom them", "-120 denarii, the pens empty without blood",
                        new PoiOutcome
                        {
                            Denarii = -120, Morale = 0.04f,
                            Recruits = 3, RecruitHealthMin = 0.2f, RecruitHealthMax = 0.35f, RecruitExperienceMax = 250,
                            ResultText = "Silver changes hands and the chains come off. The freed men fall in behind the standard."
                        }),
                    new PoiChoice("March on", "leave them to the buyers",
                        new PoiOutcome
                        {
                            Morale = -0.05f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Signifer, -0.06f) },
                            ResultText = "You march past the pens. The men keep their eyes on the road."
                        }),
                }
            });

            Add(new PoiEvent
            {
                Id = "opportunity",
                Title = "The Prize Your Scout Marked",
                Subtitle = "The Speculator's report bears fruit",
                Body = "Your scout led you here on a promise of plunder: a strongpoint, lightly held, with something " +
                       "worth the taking behind its palisade.",
                OfficerRemark = "Speculator: \"I counted few of them, sir. We'll not get a better chance.\"",
                Choices = new[]
                {
                    new PoiChoice("Seize it", "a fight, then rich spoils",
                        new PoiOutcome
                        {
                            Denarii = 90, SpawnFight = true, FightStrength = 12,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("grain_sack", 4),
                                new PoiOutcome.ItemGrant("flour_sack", 1),
                                new PoiOutcome.ItemGrant("salt_blocks", 2),
                                new PoiOutcome.ItemGrant("fur_pelts", 3),
                                new PoiOutcome.ItemGrant("amber_lumps", 1),
                            },
                            ResultText = "You storm the strongpoint for its prize."
                        }),
                    new PoiChoice("Grab what you can and go", "+35 coin, no fight",
                        new PoiOutcome { Coin = 35, ResultText = "You lift what's loose and slip away before they muster." }),
                    new PoiChoice("Decide it's not worth it", "leave the prize",
                        new PoiOutcome { ResultText = "You judge the risk too great and march on." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "false_lead",
                Title = "An Empty Steading",
                Subtitle = "The prize your scout marked",
                Body = "The palisade is real enough. Behind it: cold fires, bare stores, and cart-tracks " +
                       "three days old. Whatever was here left before the report was done being spoken.",
                OfficerRemark = "Speculator: \"They were here, sir. I'd stake my name on it. They were here.\"",
                Choices = new[]
                {
                    new PoiChoice("Search it anyway", "a thorough waste of an hour",
                        new PoiOutcome { Morale = -0.03f, ResultText = "You turn over empty sheds. The men mutter about the scout's eyes." }),
                    new PoiChoice("March on", "say nothing of it",
                        new PoiOutcome { Morale = -0.02f, ResultText = "A march for nothing. The column turns back to the road." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "opportunity_meagre",
                Title = "A Poor Prize",
                Subtitle = "The prize your scout marked",
                Body = "The strongpoint is held, as promised — but the promise flattered it. A poor steading " +
                       "behind a hurdle fence, and thin pickings behind that.",
                OfficerRemark = "Speculator: \"Smaller than it looked from the ridge, sir. But it's yours for the taking.\"",
                Choices = new[]
                {
                    new PoiChoice("Take it anyway", "a small fight, small spoils",
                        new PoiOutcome
                        {
                            Denarii = 25, SpawnFight = true, FightStrength = 6,
                            Items = new[] { new PoiOutcome.ItemGrant("grain_sack", 2) },
                            ResultText = "You take the steading for what little it holds."
                        }),
                    new PoiChoice("Not worth Roman blood", "leave it",
                        new PoiOutcome { ResultText = "You leave the hovel to its farmers." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "opportunity_rich",
                Title = "A Rich Prize",
                Subtitle = "The prize your scout marked",
                Body = "The scout undersold it. A chieftain's holding: full granaries, penned cattle, amber " +
                       "and silver behind a stout palisade — and spears enough to keep it, if you let them muster.",
                OfficerRemark = "Speculator: \"Told you it was worth the ride, sir. Now pay me the compliment of taking it.\"",
                Choices = new[]
                {
                    new PoiChoice("Storm it", "a hard fight, then a chieftain's hoard",
                        new PoiOutcome
                        {
                            Denarii = 180, SpawnFight = true, FightStrength = 20,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("grain_sack", 6),
                                new PoiOutcome.ItemGrant("flour_sack", 2),
                                new PoiOutcome.ItemGrant("salt_blocks", 3),
                                new PoiOutcome.ItemGrant("fur_pelts", 4),
                                new PoiOutcome.ItemGrant("amber_lumps", 3),
                                new PoiOutcome.ItemGrant("mead_jar", 2),
                            },
                            ResultText = "You storm the holding for everything it has."
                        }),
                    new PoiChoice("Raid the pens and run", "+70 coin, no proper fight",
                        new PoiOutcome { Coin = 70, ResultText = "You cut out what can walk and are gone before the horn sounds." }),
                    new PoiChoice("Too well held", "leave the hoard",
                        new PoiOutcome { Morale = -0.03f, ResultText = "You march away from a chieftain's hoard. The men feel the weight of it." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "signum_held",
                Title = "The Signum, Found",
                Subtitle = "The speculator has run it to ground",
                Body = "There, past the fires: the century's own standard, planted in barbarian earth like a " +
                       "trophy. The warband that took it has not stopped celebrating. Every man in the column " +
                       "can see it from the treeline.",
                OfficerRemark = "Speculator: \"I told them I'd find it, sir. The rest is soldiers' work.\"",
                Choices = new[]
                {
                    new PoiChoice("Take it back", "a hard fight — and the century's honour",
                        new PoiOutcome
                        {
                            SpawnFight = true, FightStrength = 22, FightCarriesSignum = true,
                            ResultText = "The century goes forward without a word needing to be said."
                        }),
                    new PoiChoice("Not with the strength we have", "mark it and withdraw",
                        new PoiOutcome
                        {
                            Morale = -0.06f,
                            Reactions = new[] { new PoiOutcome.OfficerReaction(PostId.Signifer, -0.10f) },
                            ResultText = "You withdraw from your own standard. No man speaks on the march back."
                        }),
                }
            });

            AddAftermathEvents(Add);
            return map;
        }

        /// <summary>
        /// THE AFTERMATH: the guided start's story places. Every survivor-yielding event hands over
        /// men in the state the massacre left them, and the looters' wagon frees exactly as many
        /// as the objective still needs.
        /// </summary>
        private static void AddAftermathEvents(System.Action<PoiEvent> Add)
        {
            Add(new PoiEvent
            {
                Id = "aftermath_corpses",
                Title = "A Wasteland of Corpses",
                Subtitle = "The massacre, a day cold",
                Body = "Legionaries lie as they fell, three deep where the line broke. Horses, mules, shields split " +
                       "like kindling. The crows have started. Anything the barbarians did not want is still here, " +
                       "and anything that hid may be here too.",
                OfficerRemark = "Valerius: \"Quietly, sir. If any of ours are alive in this, they'll not answer a shout.\"",
                Choices = new[]
                {
                    new PoiChoice("Comb the field", "salvage, and whatever hides among the dead",
                        new PoiOutcome
                        {
                            Recruits = 2, RecruitHealthMin = 0.4f, RecruitHealthMax = 0.65f, RecruitExperienceMax = 160,
                            Morale = -0.02f,
                            Items = new[]
                            {
                                new PoiOutcome.ItemGrant("scutum_spare", 1),
                                new PoiOutcome.ItemGrant("pila_bundle", 1),
                                new PoiOutcome.ItemGrant("linen_bandages", 2),
                                new PoiOutcome.ItemGrant("hardtack", 3),
                            },
                            ResultText = "Two men crawl out from beneath a dead horse, alive and shaking. You take what iron the field gives up."
                        }),
                    new PoiChoice("Search for the living only", "no salvage; the men keep their stomachs",
                        new PoiOutcome
                        {
                            Recruits = 2, RecruitHealthMin = 0.4f, RecruitHealthMax = 0.65f, RecruitExperienceMax = 160,
                            Morale = 0.02f,
                            ResultText = "Two men, hidden beneath the carcass of a horse. They weep when they see the crest."
                        }),
                }
            });

            Add(new PoiEvent
            {
                Id = "aftermath_hounds",
                Title = "Hounds on the Field",
                Subtitle = "A pack worrying the dead",
                Body = "Forest dogs gone wild on the feast snarl over the bodies. As you close, shouts break out from " +
                       "among the corpses: Roman voices. Two men have been playing dead under the pack's noses since dawn.",
                OfficerRemark = "Valerius: \"They'll not stand against shields, sir. Drive them off and get those two up.\"",
                Choices = new[]
                {
                    new PoiChoice("Drive off the hounds", "shields forward; the men take heart",
                        new PoiOutcome
                        {
                            Recruits = 2, RecruitHealthMin = 0.45f, RecruitHealthMax = 0.7f, RecruitExperienceMax = 200,
                            Morale = 0.03f,
                            ResultText = "The pack scatters before a wall of shields. Two men rise from the dead, grey with fright."
                        }),
                    new PoiChoice("Throw them the horse meat", "-6 food, no risk to the wounded",
                        new PoiOutcome
                        {
                            Food = -6f,
                            Recruits = 2, RecruitHealthMin = 0.45f, RecruitHealthMax = 0.7f, RecruitExperienceMax = 200,
                            ResultText = "The hounds fall on the meat and forget the living. Two men come out of the dead on their hands and knees."
                        }),
                }
            });

            Add(new PoiEvent
            {
                Id = "aftermath_looters",
                Title = "Looters at the Fire",
                Subtitle = "Roman men chained to a wagon",
                Body = "A fire in a hollow, and round it looters picking over a wagon of plunder. Chained to its wheel, " +
                       "Romans: to be sold when the slave-fair comes. The looters are drunk on stolen wine, but they are armed, " +
                       "and there are more of them than of you.",
                OfficerRemark = "Valerius: \"Slaves, sir. That's what we all are to them now. I say we cut those chains.\"",
                Choices = new[]
                {
                    new PoiChoice("Free them: attack the fire", "a fight; the freed men are starved and cannot fight yet",
                        new PoiOutcome
                        {
                            SpawnFight = true, FightStrength = 7,
                            FightArchetype = "germanic_looter", FightName = "Looters from the Fire",
                            RecruitsToReach = 12, RecruitHealthMin = 0.15f, RecruitHealthMax = 0.24f, RecruitExperienceMax = 220,
                            Items = new[] { new PoiOutcome.ItemGrant("grain_sack", 2), new PoiOutcome.ItemGrant("wine_amphora", 1) },
                            ResultText = "You fall on the fire with a shout. The chains come off."
                        }),
                    new PoiChoice("Wait for dark and cut them loose", "no fight; the looters keep the plunder",
                        new PoiOutcome
                        {
                            RecruitsToReach = 12, RecruitHealthMin = 0.15f, RecruitHealthMax = 0.24f, RecruitExperienceMax = 220,
                            Morale = 0.02f,
                            ResultText = "In the small hours a knife works at the chains. By first light the wagon is empty and you are gone."
                        }),
                }
            });

            Add(new PoiEvent
            {
                Id = "aftermath_safe_place",
                Title = "A Hollow in the Rocks",
                Subtitle = "Ground fit to camp on",
                Body = "A fold in the hillside, screened by rock and old pines, with a spring at the bottom. Out of the " +
                       "wind, out of sight of the road. The men are on their last legs.",
                OfficerRemark = "Valerius: \"Here, sir. Press CAMP and let them sleep. I'll show you how a camp is kept.\"",
                Choices = new[]
                {
                    new PoiChoice("Halt here", "the men drop where they stand",
                        new PoiOutcome { Morale = 0.03f, ResultText = "The men drop where they stand. A fire is lit in the lee of the rock." }),
                }
            });

            Add(new PoiEvent
            {
                Id = "aftermath_passage",
                Title = "The Passage",
                Subtitle = "An old ravine through the forested rock",
                Body = "The scout's ravine: a cleft in the high ground, stone walls hung with roots, the old track beneath " +
                       "a foot of leaf-litter. Beyond it the forest opens, and the long road home begins.",
                OfficerRemark = "Valerius: \"Through here and we're out of the killing ground, sir. Where we go after that is yours to say.\"",
                Choices = new[]
                {
                    new PoiChoice("Take the passage", "leave the Aftermath behind",
                        new PoiOutcome { Morale = 0.04f, ResultText = "The century files into the ravine." }),
                }
            });
        }
    }
}
