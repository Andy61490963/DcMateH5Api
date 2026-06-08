using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DcMateH5.Abstractions.EQM.Models
{
    public class EqmAutoDcInputDto
    {
        private string _eqmMasterNo = string.Empty;

        public string EqmMasterNo
        {
            get => string.IsNullOrEmpty(_eqmMasterNo) ? EQP_NO : _eqmMasterNo;
            set => _eqmMasterNo = value;
        }

        public string EQP_NO { get; set; } = string.Empty;
        public string AutoDcItem { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public string VALUE { get => Value; set => Value = value; }
        public string DC_ITEM { get => AutoDcItem; set => AutoDcItem = value; }

        private string _historyTableName = string.Empty;
        private string _currentTableName = string.Empty;
        private string _todayTableName = string.Empty;

        /// <summary>
        /// Optional AutoDC history table. Blank uses the service default table.
        /// </summary>
        public string HistoryTableName
        {
            get => _historyTableName;
            set => _historyTableName = value ?? string.Empty;
        }

        /// <summary>
        /// Optional AutoDC current table. Blank uses the service default table.
        /// </summary>
        public string CurrentTableName
        {
            get => _currentTableName;
            set => _currentTableName = value ?? string.Empty;
        }

        /// <summary>
        /// Optional AutoDC today-only history table. Blank skips today table writes.
        /// </summary>
        public string TodayTableName
        {
            get => _todayTableName;
            set => _todayTableName = value ?? string.Empty;
        }

        public string Table
        {
            get => HistoryTableName;
            set => HistoryTableName = value;
        }

        public string CurTable
        {
            get => CurrentTableName;
            set => CurrentTableName = value;
        }

        public string TodayTable
        {
            get => TodayTableName;
            set => TodayTableName = value;
        }

        public string TABLE { get => HistoryTableName; set => HistoryTableName = value; }
        public string CUR_TABLE { get => CurrentTableName; set => CurrentTableName = value; }
        public string TODAY_TABLE { get => TodayTableName; set => TodayTableName = value; }

        /// <summary>
        /// 資料類型模式：WIP (處理溢位特殊計算) 或 EDC (不做特殊處理，直接相減)
        /// </summary>
        public string Mode { get; set; } = "WIP";

        public string AutoIdle { get; set; } = "FALSE";
        public string SameChange { get; set; } = "FALSE";
        public string Check { get; set; } = "N";
        public string Rate { get; set; } = "1";
    }
}
