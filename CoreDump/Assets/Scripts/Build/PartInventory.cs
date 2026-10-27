using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoreDump
{
    // The parts you own but haven't fitted. The build screen shows them sorted into folders, and
    // parts taken off the robot come back here. In a run it fills from the workshop's stands; the
    // test room makes everything unlimited.
    public class PartInventory : MonoBehaviour
    {
        [Serializable]
        public struct Stock
        {
            public PartDefinition part;
            [Tooltip("How many you have. Below zero means unlimited.")]
            public int count;
        }

        [Tooltip("What you start with, in the order the folders list them.")]
        public List<Stock> stock = new();

        List<Stock> starting;

        public IReadOnlyList<Stock> Items => stock;

        void Awake() => starting = Copy();

        // How many of a part you have; below zero means unlimited.
        public int Count(PartDefinition part)
        {
            int i = stock.FindIndex(s => s.part == part);
            return i < 0 ? 0 : stock[i].count;
        }

        public bool Take(PartDefinition part)
        {
            int i = stock.FindIndex(s => s.part == part);
            if (i < 0 || stock[i].count == 0) return false;
            if (stock[i].count > 0) stock[i] = new Stock { part = part, count = stock[i].count - 1 };
            return true;
        }

        public void Add(PartDefinition part)
        {
            int i = stock.FindIndex(s => s.part == part);
            if (i < 0) stock.Add(new Stock { part = part, count = 1 });
            else if (stock[i].count >= 0) stock[i] = new Stock { part = part, count = stock[i].count + 1 };
        }

        public List<Stock> Copy() => new(stock);

        public void Restore(List<Stock> saved)
        {
            stock.Clear();
            stock.AddRange(saved);
        }

        public void ResetStock() => Restore(starting);
    }
}
