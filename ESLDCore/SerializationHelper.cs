using System;
using System.Collections.Generic;
using System.Linq;

namespace ESLDCore
{
	public static class SerializationHelper
	{
		public static List<T> LoadObjects<T>(this PartModule partModule, string nodeName, ConfigNode saveNode, int moduleIndex, string identifierKey = "name", bool removeUnsaved = false) where T : class, IConfigNode, new()
		{
			return LoadObjects<T>(nodeName, saveNode, partModule.part.partInfo.partConfig.GetNodes("MODULE", "name", partModule.GetType().Name)[moduleIndex], identifierKey, removeUnsaved);
		}

		public static List<T> LoadObjects<T>(this PartModule partModule, string nodeName, ConfigNode saveNode, string identifierKey = "name", bool removeUnsaved = false) where T : class, IConfigNode, new()
		{
			if (partModule == null || partModule.part == null ||
	partModule.part.partInfo == null || partModule.part.partInfo.partConfig == null)
				return new List<T>();

			return LoadObjects<T>(nodeName, saveNode, partModule.part.partInfo.partConfig.GetNodes("MODULE", "name", partModule.GetType().Name)[0], identifierKey, removeUnsaved);
		}

		public static List<T> LoadObjects<T>(string nodeName, ConfigNode saveNode, ConfigNode cfgNode, string identifierKey = "name", bool removeUnsaved = false) where T : class, IConfigNode, new()
		{
			List<T> list = new List<T>();
			if (!saveNode.HasNode(nodeName) && !cfgNode.HasNode(nodeName))
			{
				return list;
			}
			ConfigNode[] nodes = cfgNode.GetNodes(nodeName);
			ConfigNode[] savedNodes = saveNode.GetNodes(nodeName);
			int i;
			for (i = 0; i < nodes.Length; i++)
			{
				ConfigNode configNode = savedNodes.FirstOrDefault((ConfigNode n) => n.GetValue(identifierKey) == nodes[i].GetValue(identifierKey));
				if (!removeUnsaved || configNode != null)
				{
					T val = new T();
					val.Load(nodes[i]);
					if (configNode != null)
					{
						val.Load(configNode);
					}
					list.Add(val);
				}
			}
			int j;
			for (j = 0; j < savedNodes.Length; j++)
			{
				if (!nodes.Any((ConfigNode n) => n.GetValue(identifierKey) == savedNodes[j].GetValue(identifierKey)))
				{
					T val2 = new T();
					val2.Load(savedNodes[j]);
					list.Add(val2);
				}
			}
			return list;
		}

		public static void LoadObjects<T>(List<T> objects, string nodeName, ConfigNode node, Func<T, string> identifierSelector, string identifierKey = "name") where T : class, IConfigNode, new()
		{
			ConfigNode[] nodes = node.GetNodes(nodeName);
			int i;
			for (i = 0; i < nodes.Length; i++)
			{
				int objNum = objects.FindIndex((Predicate<T>)((T o) => identifierSelector(o) == nodes[i].GetValue(identifierKey)));
				if (objNum < 0)
				{
					T val = new T();
					objects.Add(val);
					val.Load(nodes[i]);
				}
				else
				{
					objects[objNum].Load(nodes[i]);
				}
			}
		}
	}
}
