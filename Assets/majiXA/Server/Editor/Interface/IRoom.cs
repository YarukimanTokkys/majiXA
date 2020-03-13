﻿using System.Collections.Generic;

namespace majiXA
{
    public interface IRoom
    {
        int RoomId { get; }
        List<string> MembersCid { get; }
        int Capacity { get; }
        bool InGame { get; set; }

        void OnUpdate();

        int Join(ConnectionInfo cInfo);

        int Leave(ConnectionInfo cInfo);
    }
}