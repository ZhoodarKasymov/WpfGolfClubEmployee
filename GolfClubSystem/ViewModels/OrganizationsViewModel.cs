using System.Collections.ObjectModel;

namespace GolfClubSystem.ViewModels
{
    public class Node
    {
        public Node()
        {
            Nodes = new ObservableCollection<Node>();
        }

        public string Name { get; set; }
        public ObservableCollection<Node> Nodes { get; set; }
    }

    public enum NodeType
    {
        AddNode,
        AddSubNode,
        EditNode
    }
}