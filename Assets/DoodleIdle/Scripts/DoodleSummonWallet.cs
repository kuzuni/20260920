using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed class DoodleSummonWallet : MonoBehaviour
    {
        DoodleUi ui;
        string category;
        Text tickets, diamonds;
        int shownTickets = -1, shownDiamonds = -1;

        public void Configure(DoodleUi source, string summonCategory, Text ticketLabel, Text diamondLabel)
        {
            ui = source; category = summonCategory; tickets = ticketLabel; diamonds = diamondLabel;
            Refresh();
        }

        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (!ui) return;
            int currentTickets = ui.SummonTickets(category), currentDiamonds = ui.Diamonds;
            if (currentTickets != shownTickets) { shownTickets = currentTickets; tickets.text = currentTickets.ToString("N0") + "장"; }
            if (currentDiamonds != shownDiamonds) { shownDiamonds = currentDiamonds; diamonds.text = currentDiamonds.ToString("N0") + "개"; }
        }
    }
}
