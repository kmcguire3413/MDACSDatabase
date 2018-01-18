#define DOUBLE_ENDED_STREAM_DEBUG

using System;

namespace MDACS.Database
{
    internal class JournalHashException: ProgramException
    {
        public JournalHashException(String msg) : base(msg)
        {
        }
    }
}
