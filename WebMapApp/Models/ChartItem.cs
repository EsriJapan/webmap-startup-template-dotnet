using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebMapApp.Models
{
    /// <summary>
    /// グラフに表示するアイテム
    /// </summary>
    public class ChartItem
    {
        /// <summary>
        /// アイテムの OBJECT ID
        /// </summary>
        public long ObjectId { get; set; }

        /// <summary>
        /// アイテムの値
        /// </summary>
        public double ItemValue { get; set; }
    }
}
