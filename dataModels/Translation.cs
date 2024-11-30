using System;

namespace moji.DataModels
{
    public class Translation
    {
        public int Id { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public DateTime CreatedAt { get; set; }

        public Translation()
        {
            OriginalText = string.Empty;
            TranslatedText = string.Empty;
            CreatedAt = DateTime.Now;
        }
    }
}