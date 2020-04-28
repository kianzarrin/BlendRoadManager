namespace BlendRoadManager {
    using System;
    using System.IO;
    using System.Runtime.Serialization.Formatters;
    using System.Runtime.Serialization.Formatters.Binary;
    using Util;

    [Serializable]
    public class NodeManager {
        #region LifeCycle
        public static NodeManager Instance { get; private set; } = new NodeManager();

        static BinaryFormatter GetBinaryFormatter =>
            new BinaryFormatter { AssemblyFormat = FormatterAssemblyStyle.Simple };

        public static void Deserialize(byte[] data) {
            if (data == null) {
                Instance = new NodeManager();
                Log.Debug($"NodeBlendManager.Deserialize(data=null)");
                return;
            }
            Log.Debug($"NodeBlendManager.Deserialize(data): data.Length={data?.Length}");

            var memoryStream = new MemoryStream();
            memoryStream.Write(data, 0, data.Length);
            memoryStream.Position = 0;
            Instance = GetBinaryFormatter.Deserialize(memoryStream) as NodeManager;
            //Instance.UpdateAllNodes();
        }

        public static byte[] Serialize() {
            var memoryStream = new MemoryStream();
            GetBinaryFormatter.Serialize(memoryStream, Instance);
            memoryStream.Position = 0; // redundant
            return memoryStream.ToArray();
        }
        #endregion LifeCycle

        public NodeData[] buffer = new NodeData[NetManager.MAX_NODE_COUNT];

        public NodeData GetOrCreate(ushort nodeID) {
            NodeData data = NodeManager.Instance.buffer[nodeID];
            if (data == null) {
                data = new NodeData(nodeID);
                NodeManager.Instance.buffer[nodeID] = data;
            }
            return data;
        }

        /// <summary>
        /// releases data for <paramref name="nodeID"/> if uncessary. Calls update node.
        /// </summary>
        /// <param name="nodeID"></param>
        public void RefreshData(ushort nodeID) {
            if (nodeID == 0 || buffer[nodeID] == null)
                return;
            if (buffer[nodeID].IsDefault()) {
                Log.Info($"node reset to defualt");
                buffer[nodeID] = null;
                NetManager.instance.UpdateNode(nodeID);
            } else {
                buffer[nodeID].Refresh();
            }
        }

        public void UpdateAllNodes() {
            foreach (var blendData in buffer)
                blendData?.Refresh();
        }


        //public void ChangeNode(ushort nodeID) {
        //    Log.Info($"ChangeNode({nodeID}) called");
        //    NodeBlendData data = GetOrCreate(nodeID);
        //    data.ChangeNodeType();
        //    Instance.buffer[nodeID] = data;
        //    RefreshData(nodeID);
        //}


        //public void ChangeOffset(ushort nodeID) {
        //    Log.Info($"ChangeOffset({nodeID}) called");
        //    if (!nodeID.ToNode().m_flags.IsFlagSet(NetNode.Flags.Junction)) {
        //        Log.Info($"Not a junction");
        //        return;
        //    }

        //    NodeBlendData data = GetOrCreate(nodeID);
        //    if (!data.CanModifyOffset())
        //        return;

        //    data.IncrementOffset();
        //    Instance.buffer[nodeID] = data;
        //    RefreshData(nodeID);
        //}
    }
}