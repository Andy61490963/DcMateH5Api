using System;

namespace DcMateH5Api.Areas.Form.ViewModels
{
    public class UpdateFormNameViewModel
    {
        public Guid ID { get; set; }

        /// <summary>
        /// 主檔名稱
        /// </summary>
        public string FORM_NAME { get; set; }

        /// <summary>
        /// 主檔代碼
        /// </summary>
        public string FORM_CODE { get; set; }
    
        /// <summary>
        /// 主檔設定描述
        /// </summary>
        public string FORM_DESCRIPTION { get; set; }
    }
}

