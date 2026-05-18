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
