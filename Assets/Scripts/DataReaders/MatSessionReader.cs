using System;
using UnityEngine;
using VirtualMaze.Assets.Scripts.DataReaders;

namespace VirtualMaze.Assets.Scripts.DataReaders {

    public class MatSessionReader : SessionDataReader {
        private readonly UnityMazeMatFile file;

        private int index = 0;
        public int CurrentIndex => index;

        // index smaller than row length
        public bool HasNext => index < numTrial;

        public float ReadProgress => (float)index / numTrial;


        private readonly int[] trialIndex;
        public readonly int numTrial;

        public MatSessionReader(UnityMazeMatFile file) {
            this.file = file;
            numTrial = file.unityData.GetLength(1);

            Debug.LogWarning(numTrial);

            trialIndex = new int[file.unityTriggersIndex.GetLength(1)];
            for (int i = 0; i < trialIndex.Length - 1; i++) {
                //store the index of all starttrial triggers
                trialIndex[i] = (int)file.unityTriggersIndex[0, i];
            }
        }

        public MatSessionReader(string filePath) : this(new UnityMazeMatFile(filePath)) {
        }

        public override void Dispose() {
            file.Dispose();
        }



        private int GetTrialIndexPosition() {
            return Array.BinarySearch(trialIndex, index);
        }

        public override bool Next() {
            if (HasNext) {
                index++;
                try {
                    int flag = (int)file.unityData[0, index];
                    decimal timeDelta = Convert.ToDecimal(file.unityData[1, index]);
                    double posX = file.unityData[2, index];
                    double posZ = file.unityData[3, index];
                    double rotY = file.unityData[4, index];

                    CurrentData = new SessionData(flag, timeDelta, posX, posZ, rotY);

                }
                catch {
                    Debug.Log(CurrentIndex);
                }

                return true;
            }
            return false;
        }


    }
}