using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace OSIResearch.Qillion.Functions
{
    public class QiStreamEntity
    {
        public string id { get; set; }
        public int batchid { get; set; }
    }
}
