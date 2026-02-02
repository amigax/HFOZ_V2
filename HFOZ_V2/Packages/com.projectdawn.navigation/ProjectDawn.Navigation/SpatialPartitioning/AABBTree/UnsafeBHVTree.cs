using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

// Taken from my other package, Dots Plus.
// For similar helper functions, see https://assetstore.unity.com/packages/tools/utilities/dots-plus-227492
//namespace ProjectDawn.Navigation.LowLevel.Unsafe
namespace ProjectDawn.Navigation.LowLevel.Unsafe
{
    /// <summary>
    /// An unmanaged, resizable bhv tree, without any thread safety check features.
    /// BHV tree (short for bounding hierarchy volume tree) is a space-partitioning data structure for organizing bounding shapes in space.
    /// As structure uses generic it is not only usable for boxes, but any shape that implements interfaces.
    /// Based on https://box2d.org/files/ErinCatto_DynamicBVH_GDC2019.pdf.
    /// </summary>
    /// <typeparam name="TVolume">The type of the bounding volume.</typeparam>
    /// <typeparam name="TValue">The stored value in accelerated structure.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UnsafeBHVTree<TVolume, TValue>
        : IDisposable
        where TVolume : unmanaged, ISurfaceArea<TVolume>, IUnion<TVolume>
        where TValue : unmanaged
    {
        UnsafeList<Node> m_Nodes;
        UnsafeStack<int> m_FreeHandles;
        UnsafeHeap<float, int> m_FindBestHandleQueue;
        int m_Length;
        int m_RootHandle;

        /// <summary>
        /// Whether the tree is empty.
        /// </summary>
        /// <value>True if the tree is empty or the tree has not been constructed.</value>
        public bool IsEmpty => !IsCreated || Length == 0;

        /// <summary>
        /// The number of elements.
        /// </summary>
        /// <value>The number of elements.</value>
        public int Length => m_Length;

        /// <summary>
        /// The number of elements that fit in the current allocation.
        /// </summary>
        /// <value>The number of elements that fit in the current allocation.</value>
        public int Capacity => m_Nodes.Capacity;

        /// <summary>
        /// Whether this tree has been allocated (and not yet deallocated).
        /// </summary>
        /// <value>True if this tree has been allocated (and not yet deallocated).</value>
        public bool IsCreated => m_Nodes.IsCreated && m_FreeHandles.IsCreated;

        /// <summary>
        /// Returns the tree root handle.
        /// </summary>
        public Handle Root => new Handle(m_RootHandle);

        /// <summary>
        /// The bode at a given handle.
        /// </summary>
        /// <param name="handle">Handle of the element.</param>
        public Node this[Handle handle]
        {
            get
            {
                CheckHandle(handle);
                return m_Nodes.Ptr[handle];
            }
        }

        public Node GetNode(Handle handle)
        {
            CheckHandle(handle);
            return m_Nodes.Ptr[handle];
        }

        /// <summary>
        /// Initialized and returns an instance of NativeAABBTree.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the priority queue.</param>
        /// <param name="allocator">The allocator to use.</param>
        public UnsafeBHVTree(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            m_Nodes = new UnsafeList<Node>(initialCapacity, allocator);
            m_FreeHandles = new UnsafeStack<int>(initialCapacity, allocator);
            m_FindBestHandleQueue = new UnsafeHeap<float, int>(1, allocator);
            m_Length = 0;
            m_RootHandle = Node.Null;
        }

        /// <summary>
        /// Creates a new container with the specified initial capacity and type of memory allocation.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the list. If the list grows larger than its capacity,
        /// the internal array is copied to a new, larger array.</param>
        /// <param name="allocator">A member of the
        /// [Unity.Collections.Allocator](https://docs.unity3d.com/ScriptReference/Unity.Collections.Allocator.html) enumeration.</param>
        public static UnsafeBHVTree<TVolume, TValue>* Create(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            UnsafeBHVTree<TVolume, TValue>* data = AllocatorManager.Allocate<UnsafeBHVTree<TVolume, TValue>>(allocator);
            *data = new UnsafeBHVTree<TVolume, TValue>(initialCapacity, allocator);
            return data;
        }

        /// <summary>
        /// Destroys container.
        /// </summary>
        public static void Destroy(UnsafeBHVTree<TVolume, TValue>* data)
        {
            CollectionChecks.CheckNull(data);
            var allocator = data->m_Nodes.Allocator;
            data->Dispose();
            AllocatorManager.Free(allocator, data);
        }

        /// <summary>
        /// Add element to the tree.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <param name="rebalance">Should it attempt to rebalance.</param>
        public Handle Add(TVolume volume, TValue value, bool rebalance = true)
        {
            // Create root if it does not exist
            if (m_RootHandle == Node.Null)
            {
                m_RootHandle = Allocate(volume, value);
                m_Length = 1;
                Node* rootNode = m_Nodes.Ptr + m_RootHandle;
                rootNode->m_ParentHandle = Node.Null;
                rootNode->m_LeftChildHandle = Node.Null;
                rootNode->m_RightChildHandle = Node.Null;
                return new Handle(m_RootHandle);
            }

            // Stage 1: find the best sibling for the new leaf
            // This handle will have the lowest cost to insert new value
            m_FindBestHandleQueue.Clear();
            int bestHandle = FindBestHandle(volume, ref m_FindBestHandleQueue);

            var newLeafHandle = Allocate(volume, value);
            var newParentHandle = Allocate(default, default);

            Node* bestNode = m_Nodes.Ptr + bestHandle;
            Node* newLeafNode = m_Nodes.Ptr + newLeafHandle;
            Node* newParentNode = m_Nodes.Ptr + newParentHandle;

            // Stage 2: create a new parent
            // Add newParentNode and set their childs bestNode and newLeafNode
            if (bestHandle == m_RootHandle)
            {
                m_RootHandle = newParentHandle;
                newParentNode->m_ParentHandle = Node.Null;
            }
            else
            {
                int grandParentHandle = bestNode->m_ParentHandle;
                Node* grandParentNode = m_Nodes.Ptr + grandParentHandle;

                // Update grand parent connections
                if (grandParentNode->m_LeftChildHandle == bestHandle)
                    grandParentNode->m_LeftChildHandle = newParentHandle;
                else
                    grandParentNode->m_RightChildHandle = newParentHandle;

                newParentNode->m_ParentHandle = grandParentHandle;
            }

            // Update new parent connections
            newParentNode->m_LeftChildHandle = bestHandle;
            newParentNode->m_RightChildHandle = newLeafHandle;

            bestNode->m_ParentHandle = newParentHandle;

            // Update new leaf connections
            newLeafNode->m_ParentHandle = newParentHandle;
            newLeafNode->m_LeftChildHandle = Node.Null;
            newLeafNode->m_RightChildHandle = Node.Null;

            // Stage 3: walk back up the tree refitting AABBs
            int handle = newParentHandle;
            while (handle != Node.Null)
            {
                Node* node = m_Nodes.Ptr + handle;
                node->m_Volume = Union(m_Nodes.Ptr[node->m_LeftChildHandle].m_Volume, m_Nodes.Ptr[node->m_RightChildHandle].m_Volume);

                // Do tree rotation to minimize the SAH cost
                // Source: https://box2d.org/files/ErinCatto_DynamicBVH_GDC2019.pdf
                if (rebalance)
                    Rotate(handle);

                handle = node->m_ParentHandle;
            }

            m_Length++;

            return new Handle(newLeafHandle);
        }

        /// <summary>
        /// Removes node from the tree at givent iterator value.
        /// </summary>
        /// <param name="iterator">Position from which node will be removed.</param>
        public void RemoveAt(Handle iterator)
        {
            int handle = iterator;
            CheckHandle(handle);
            CheckLeafHandle(handle);
            Node* node = m_Nodes.Ptr + handle;

            // Remove root if node is root
            if (handle == m_RootHandle)
            {
                // Only leaf removing is allowed as result removing root will indicate it is last node
                m_RootHandle = Node.Null;
                m_Length = 0;
                Free(handle);
                return;
            }

            int parentHandle = node->m_ParentHandle;
            Node* parentNode = m_Nodes.Ptr + parentHandle;

            // Find other parent child
            int siblingHandle = parentNode->m_LeftChildHandle == handle ? parentNode->m_RightChildHandle : parentNode->m_LeftChildHandle;
            Node* siblingNode = m_Nodes.Ptr + siblingHandle;

            // Remove parentNode and node
            if (parentHandle == m_RootHandle)
            {
                m_RootHandle = siblingHandle;
                siblingNode->m_ParentHandle = Node.Null;
            }
            else
            {
                int grandParentHandle = parentNode->m_ParentHandle;
                Node* grandParentNode = m_Nodes.Ptr + grandParentHandle;

                // Update grand parent connections
                if (grandParentNode->m_LeftChildHandle == parentHandle)
                    grandParentNode->m_LeftChildHandle = siblingHandle;
                else
                    grandParentNode->m_RightChildHandle = siblingHandle;

                siblingNode->m_ParentHandle = grandParentHandle;
            }

            Free(handle);
            Free(parentHandle);

            m_Length--;
        }

        /// <summary>
        /// Returns the sum of all non leaf surface area.
        /// The lower the number is, the more optimal a tree will be.
        /// </summary>
        public float Cost()
        {
            float cost = 0;
            CostRecursive(m_Nodes.Ptr + m_RootHandle, ref cost);
            return cost;
        }

        void CostRecursive(Node* node, ref float cost)
        {
            if (node->IsLeaf)
                return;

            cost += node->m_Volume.SurfaceArea();

            CostRecursive(m_Nodes.Ptr + node->m_LeftChildHandle, ref cost);
            CostRecursive(m_Nodes.Ptr + node->m_RightChildHandle, ref cost);
        }

        /// <summary>
        /// Returns the number leaf nodes in this aabb tree.
        /// </summary>
        /// <returns>Returns the number leaf nodes in this aabb tree.</returns>
        public int CountLeafs()
        {
            int count = 0;
            CountLeafsRecursive(m_Nodes.Ptr + m_RootHandle, ref count);
            return count;
        }

        void CountLeafsRecursive(Node* node, ref int count)
        {
            if (node->IsLeaf)
            {
                count++;
            }
            else
            {
                CountLeafsRecursive(m_Nodes.Ptr + node->m_LeftChildHandle, ref count);
                CountLeafsRecursive(m_Nodes.Ptr + node->m_RightChildHandle, ref count);
            }
        }

        /// <summary>
        /// Returns the number nodes in this tree.
        /// </summary>
        /// <returns>Returns the number nodes in this tree.</returns>
        public int CountNodes()
        {
            int count = 0;
            CountNodesRecursive(m_Nodes.Ptr + m_RootHandle, ref count);
            return count;
        }

        void CountNodesRecursive(Node* node, ref int count)
        {
            count++;

            if (!node->IsLeaf)
            {
                CountNodesRecursive(m_Nodes.Ptr + node->m_LeftChildHandle, ref count);
                CountNodesRecursive(m_Nodes.Ptr + node->m_RightChildHandle, ref count);
            }
        }

        /// <summary>
        /// Returns the depth of the tree. It is the maximum height of all nodes.
        /// </summary>
        public int GetDepth()
        {
            int depth = 0;
            GetDepthRecursive(m_Nodes.Ptr + m_RootHandle, 0, ref depth);
            return depth;
        }

        void GetDepthRecursive(Node* node, int depth, ref int maxDepth)
        {
            if (node->IsLeaf)
            {
                if (maxDepth < depth)
                    maxDepth = depth;
            }
            else
            {
                depth++;
                GetDepthRecursive(m_Nodes.Ptr + node->m_LeftChildHandle, depth, ref maxDepth);
                GetDepthRecursive(m_Nodes.Ptr + node->m_RightChildHandle, depth, ref maxDepth);
            }
        }

        /// <summary>
        /// Returns factor from zero to one. Where one represents if tree is balanced and zero is unbalanced.
        /// This value can be used to decide if tree needs balancing.
        /// </summary>
        /// <returns>Returns factor from zero to one. Where zero represents if tree is balanced and one is unbalanced.</returns>
        public float GetBalancedTreeFactor()
        {
            if (m_Length <= 1)
                return 0;

            int nodes = CountNodes();

            // Returns depth of balanced tree with leafCount
            int minDepth = GetBalancedDepth(nodes);

            // Returns depth of unbalanced tree with leafeCount
            int maxDepth = (nodes - 1) / 2;

            if (maxDepth == minDepth)
                return 1.0f;

            // Using depth we can find where it is in range
            int depth = GetDepth();

            Assert.IsTrue(minDepth <= depth);
            Assert.IsTrue(depth <= maxDepth);

            float factor = (float)(depth - minDepth) / (maxDepth - minDepth);

            return 1.0f - factor;
        }

        static int GetBalancedDepth(int leafCount)
        {
            return (int)UnityEngine.Mathf.Log(leafCount, 2);
        }

        /// <summary>
        /// Removes all nodes of this tree.
        /// </summary>
        /// <remarks>Does not change the capacity.</remarks>
        public void Clear()
        {
            m_Nodes.Clear();
            m_FreeHandles.Clear();
            m_Length = 0;
            m_RootHandle = Node.Null;
        }

        /// <summary>
        /// Releases all resources (memory and safety handles).
        /// </summary>
        public void Dispose()
        {
            m_Nodes.Dispose();
            m_FreeHandles.Dispose();
            m_FindBestHandleQueue.Dispose();
        }

        void Rotate(int handle)
        {
            if (Root == handle)
                return;

            var current = m_Nodes.Ptr + handle;

            var parentHandle = current->m_ParentHandle;
            var parent = m_Nodes.Ptr + parentHandle;

            var isBrotherOnRight = parent->m_LeftChildHandle == handle;
            var brother = m_Nodes.Ptr + (isBrotherOnRight ? parent->m_RightChildHandle : parent->m_LeftChildHandle);

            if (Root == parentHandle)
                return;

            var grandParentHandle = parent->m_ParentHandle;
            var grandParent = m_Nodes.Ptr + parent->m_ParentHandle;

            if (grandParent->IsLeaf)
                return;

            var isUncleOnRight = grandParent->m_LeftChildHandle == parentHandle;
            var uncleHandle = (isUncleOnRight ? grandParent->m_RightChildHandle : grandParent->m_LeftChildHandle);
            var uncle = m_Nodes.Ptr + uncleHandle;

            var balanced = Union(uncle->m_Volume, brother->m_Volume);

            var currentSurfaceArea = current->m_Volume.SurfaceArea();
            var balancedSurfaceArea = balanced.SurfaceArea();

            // Surface area should be unchanged
            if (currentSurfaceArea < balancedSurfaceArea)
                return;

            // Rotate current with uncle
            if (isUncleOnRight)
            {
                grandParent->m_RightChildHandle = handle;
                current->m_ParentHandle = grandParentHandle;
            }
            else
            {
                grandParent->m_LeftChildHandle = handle;
                current->m_ParentHandle = grandParentHandle;
            }

            if (isBrotherOnRight)
            {
                parent->m_LeftChildHandle = uncleHandle;
                uncle->m_ParentHandle = parentHandle;
            }
            else
            {
                parent->m_RightChildHandle = uncleHandle;
                uncle->m_ParentHandle = parentHandle;
            }

            parent->m_Volume = balanced;
        }

        /// <summary>
        /// Returns handle that would be best to insert this new value.
        /// Inserting to this handle will have the lowest <see cref="Cost"/> compared to other leaf nodes.
        /// </summary>
        int FindBestHandle(TVolume value, ref UnsafeHeap<float, int> branches)
        {
            // Add as first root
            int bestHandle = m_RootHandle;
            float bestCost = float.MaxValue;

            if (Length == 1)
                return bestHandle;

            Assert.IsTrue(branches.IsEmpty);

            branches.Push(bestCost, bestHandle);
            while (branches.TryPop(out int handle))
            {
                Node* node = m_Nodes.Ptr + handle;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (node->IsFree)
                    throw new ArgumentException($"AABBTree is corrupted, using already deallocated node.");
#endif

                // The direct cost is the surface area of the new internal node that will be created for the siblings
                float directCost = SurfaceArea(node->m_Volume, value);

                // The inherited cost is the increased surface area caused by refitting the ancestor�s boxes
                float inheritedCost = InheritedCost(node->m_ParentHandle, value);

                // Here is the cost of inserting this new value
                float cost = directCost + inheritedCost;

                // Update best node
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestHandle = handle;
                }

                if (!node->IsLeaf)
                {
                    // Compare lower bound cost with best cost to find out if its worth to check child nodes
                    float lowerBoundCost = value.SurfaceArea() + DeltaSurfaceArea(node->m_Volume, value) + inheritedCost;
                    if (lowerBoundCost < bestCost)
                    {
                        branches.Push(lowerBoundCost, node->m_LeftChildHandle);
                        branches.Push(lowerBoundCost, node->m_RightChildHandle);
                    }
                }
            }
            return bestHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float InheritedCost(int handle, TVolume value)
        {
            float cost = 0;
            while (handle != Node.Null)
            {
                Node* node = m_Nodes.Ptr + handle;
                cost += DeltaSurfaceArea(node->m_Volume, value);
                handle = node->m_ParentHandle;
            }
            return cost;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float DeltaSurfaceArea(TVolume a, TVolume b) => a.Union(b).SurfaceArea() - a.SurfaceArea();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float SurfaceArea(TVolume a, TVolume b) => a.Union(b).SurfaceArea();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        TVolume Union(TVolume a, TVolume b) => a.Union(b);

        /// <summary>
        /// Returns new allocated node handle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Allocate(in TVolume volume, in TValue value)
        {
            int handle;
            if (m_FreeHandles.TryPop(out handle))
            {
                m_Nodes[handle] = new Node
                {
                    m_Volume = volume,
                    m_Value = value,
                };
            }
            else
            {
                handle = m_Nodes.Length;
                m_Nodes.Add(new Node
                {
                    m_Volume = volume,
                    m_Value = value,
                });
            }

            return handle;
        }

        /// <summary>
        /// Releases node with given handle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Free(int nodeHandle)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Nodes.Ptr[nodeHandle].IsFree = true;
#endif
            m_FreeHandles.Push(nodeHandle);
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckLeafHandle(int handle)
        {
            if (!m_Nodes[handle].IsLeaf)
                throw new ArgumentException($"Handle referencing {handle} is not leaf. Only leafs can be removed.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void CheckHandle(int handle)
        {
            if (handle > m_Nodes.Length || handle < 0)
                throw new ArgumentException($"Handle is not valid with {handle}.");
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Nodes[handle].IsFree)
                throw new ArgumentException($"Handle referencing {handle} that is already removed.");
#endif
        }

        /// <summary>
        /// Kd Tree iterator.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        [DebuggerDisplay("{m_Handle}")]
        public struct Handle : IEquatable<Handle>
        {
            int m_Handle;

            /// <summary>
            /// Returns true if handle is valid.
            /// </summary>
            public bool IsValid => m_Handle != Node.Null;

            public static Handle Null => new Handle(Node.Null);

            internal Handle(int handle)
            {
                m_Handle = handle;
            }

            public override bool Equals(object obj) => obj is Handle other && Equals(other);

            public override int GetHashCode() => m_Handle;

            public bool Equals(Handle other) => m_Handle == other.m_Handle;

            public static implicit operator int(Handle handled) => handled.m_Handle;
            public static bool operator ==(Handle lhs, Handle rhs) => lhs.m_Handle == rhs.m_Handle;
            public static bool operator !=(Handle lhs, Handle rhs) => lhs.m_Handle != rhs.m_Handle;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct Node
        {
            internal TVolume m_Volume;
            internal TValue m_Value;
            internal int m_ParentHandle;
            internal int m_LeftChildHandle;
            internal int m_RightChildHandle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal bool IsFree;
#endif

            public TVolume Volume => m_Volume;
            public TValue Value => m_Value;
            public Handle Parent => new Handle(m_ParentHandle);
            public Handle LeftChild => new Handle(m_LeftChildHandle);
            public Handle RightChild => new Handle(m_RightChildHandle);
            public bool IsLeaf => m_LeftChildHandle == Null;

            internal static int Null => -1;
        }
    }
}
