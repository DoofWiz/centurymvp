using System;
using Century.Campaign.Model;
using Century.Campaign.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Century.Campaign.View
{
    /// <summary>
    /// Builds the office "tradition cards" shared by both surfaces of the army layer: the camp's
    /// THE OFFICES tab (interactive — pass an invest handler) and the ARMY screen (read-only —
    /// pass null). One builder so an office looks identical wherever the player meets it.
    /// </summary>
    /// <remarks>
    /// The card is the UX answer to "no percentage soup behind a hover": every node's effect is
    /// printed on the card, rank order runs down a visible rail, and exactly three states exist —
    /// held (solid gold), ready to invest (lit), locked (dim). A vacant office keeps its card so
    /// the hole in the establishment is a thing you look at, not a missing row.
    /// </remarks>
    public static class OfficeCards
    {
        /// <summary>A wrapping row of one card per Tier-1 office, Speculator included. Pass
        /// <paramref name="onInspect"/> to make each holder's name a link to the man himself.</summary>
        public static VisualElement BuildRow(
            CampaignState state, Roster roster,
            Action<PostNodeDef> onInvest, Action<SoldierRecord> onInspect = null)
        {
            var row = new VisualElement();
            row.AddToClassList("office-row");

            foreach (string post in PostId.Tier1)
                row.Add(BuildCard(state, roster, post, onInvest, onInspect));

            return row;
        }

        /// <summary>One office's card, used standalone by the camp's office overlay.</summary>
        public static VisualElement BuildCard(
            CampaignState state, Roster roster, string post,
            Action<PostNodeDef> onInvest, Action<SoldierRecord> onInspect = null)
        {
            state.Posts.EnsureSeeded(roster, state.Clock.Now.DayNumber, state.PlayerParty?.Appointments);

            PostRecord record = state.Posts.Find(post);
            SoldierRecord holder = state.Posts.HolderOf(post, roster);
            bool vacant = holder == null;

            var card = new VisualElement();
            card.AddToClassList("office-card");
            card.EnableInClassList("office-card--vacant", vacant);

            var postName = new Label(PostRoster.DisplayName(post).ToUpperInvariant());
            postName.AddToClassList("office-card__post");
            card.Add(postName);

            string holderText = vacant
                ? "THE OFFICE STANDS EMPTY"
                : holder.DisplayName + (holder.IsWounded ? "  ·  WOUNDED" : string.Empty);

            if (!vacant && onInspect != null)
            {
                // The holder's name is a door to the man: his record, his bonds, his grudges.
                var holderButton = new Button { text = holderText };
                holderButton.AddToClassList("office-card__holder");
                holderButton.AddToClassList("office-card__holder--link");
                SoldierRecord captured = holder;
                holderButton.clicked += () => onInspect(captured);
                card.Add(holderButton);
            }
            else
            {
                var holderLabel = new Label(holderText);
                holderLabel.AddToClassList("office-card__holder");
                if (vacant) holderLabel.AddToClassList("office-card__holder--vacant");
                card.Add(holderLabel);
            }

            var charge = new Label(PostRoster.Charge(post));
            charge.AddToClassList("office-card__charge");
            card.Add(charge);

            if (vacant)
            {
                var penalty = new Label(PostRoster.VacancyPenalty(post));
                penalty.AddToClassList("office-card__penalty");
                card.Add(penalty);
            }

            // The lost signum is the signifer's shame to wear until the century wins it back.
            if (post == PostId.Signifer && state.SignumLost)
            {
                var lost = new Label("THE SIGNUM IS LOST — no standard steadies the line until it is won back");
                lost.AddToClassList("office-card__penalty");
                card.Add(lost);
            }

            // Points strip — the card's call to action, or a line on where points come from.
            int points = record != null ? Mathf.FloorToInt(record.UnspentPoints) : 0;
            var pointsLabel = new Label(
                vacant ? "AN EMPTY OFFICE LEARNS NOTHING"
                : points > 0 ? (points == 1 ? "1 POINT TO INVEST" : $"{points} POINTS TO INVEST")
                : post == PostId.Speculator ? "POINTS ARE WON RANGING AHEAD"
                : "POINTS ARE WON IN BATTLE");
            pointsLabel.AddToClassList("office-card__points");
            if (points <= 0 || vacant) pointsLabel.AddToClassList("office-card__points--none");
            card.Add(pointsLabel);

            // The tradition track: a rail with the four nodes in rank order, capstone last.
            var track = new VisualElement();
            track.AddToClassList("office-track");

            var rail = new VisualElement { pickingMode = PickingMode.Ignore };
            rail.AddToClassList("office-track__rail");
            track.Add(rail);

            foreach (PostNodeDef node in PostTreeCatalog.For(post))
                track.Add(BuildNode(state, roster, record, node, vacant, onInvest));

            card.Add(track);

            if (vacant)
            {
                var note = new Label("Appoint a man from THE CENTURY chart.");
                note.AddToClassList("office-card__note");
                card.Add(note);
            }

            return card;
        }

        private static VisualElement BuildNode(
            CampaignState state, Roster roster, PostRecord record,
            PostNodeDef node, bool vacant, Action<PostNodeDef> onInvest)
        {
            bool held = record != null && record.InvestedNodeIds.Contains(node.Id);
            bool ready = !held && !vacant && PostTreeCatalog.CanInvest(state, roster, node);
            bool interactive = ready && onInvest != null;

            // Only a node the player can actually buy right here is a Button; everything else is
            // inert, so the single clickable thing on a card is also the single lit thing.
            VisualElement row = interactive ? new Button() : new VisualElement();
            row.AddToClassList("trad-node");
            row.EnableInClassList("trad-node--held", held);
            row.EnableInClassList("trad-node--ready", ready);
            row.EnableInClassList("trad-node--locked", !held && !ready);

            var marker = new VisualElement { pickingMode = PickingMode.Ignore };
            marker.AddToClassList("trad-node__marker");
            row.Add(marker);

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("trad-node__body");

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("trad-node__head");

            var rank = new Label(RankNumeral(node.Rank)) { pickingMode = PickingMode.Ignore };
            rank.AddToClassList("trad-node__rank");
            head.Add(rank);

            var name = new Label(node.Name.ToUpperInvariant()) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("trad-node__name");
            head.Add(name);

            var cost = new Label(
                held ? "HELD"
                : interactive ? $"INVEST · {node.Cost}"
                : $"{node.Cost} PTS") { pickingMode = PickingMode.Ignore };
            cost.AddToClassList("trad-node__cost");
            head.Add(cost);

            body.Add(head);

            var effect = new Label(node.Effect) { pickingMode = PickingMode.Ignore };
            effect.AddToClassList("trad-node__effect");
            body.Add(effect);

            row.Add(body);

            if (interactive)
                ((Button)row).clicked += () => onInvest(node);

            return row;
        }

        private static string RankNumeral(int rank)
        {
            switch (rank)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                default: return "IV";
            }
        }
    }
}
