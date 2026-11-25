using System.Collections.Generic;
using MechCore.Models.Threads;

namespace MechCore.Services.Threads
{
    public interface IThreadService
    {
        List<ThreadTypeItem> GetThreadTypes();
        List<ThreadSpecItem> GetSpecsByType(ThreadType type);
    }
}
