﻿using Xceed.Words.NET;

namespace Gui
{
    public static class Helper
    {
        public static Paragraph CreateHeadingSection(this InsertBeforeOrAfter paragraph, string title)
        {
            return paragraph.InsertParagraphAfterSelf(title)
                .Heading(HeadingType.Heading1);
        }
    }
}
