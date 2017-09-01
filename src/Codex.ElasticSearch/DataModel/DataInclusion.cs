using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Storage.DataModel
{
    internal static class DataInclusion
    {
        /// <summary>
        /// Removing definitions from inclusion to
        /// </summary>
        public static readonly DataInclusionOptions Options = GetDataInclusion();

        public static DataInclusionOptions GetDataInclusion()
        {
            var dataInclusionValue = Environment.GetEnvironmentVariable("DataInclusion");

            if (!string.IsNullOrEmpty(dataInclusionValue))
            {
                DataInclusionOptions options = DataInclusionOptions.None;
                foreach (var option in dataInclusionValue.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()))
                {
                    options |= (DataInclusionOptions)Enum.Parse(typeof(DataInclusionOptions), option);
                }

                Console.WriteLine("DataInclusion={0}", options);
                return options;
            }

            return DataInclusionOptions.Default;
        }

        public static bool HasOption(DataInclusionOptions option)
        {
            return (Options & option) == option;
        }
    }
}
