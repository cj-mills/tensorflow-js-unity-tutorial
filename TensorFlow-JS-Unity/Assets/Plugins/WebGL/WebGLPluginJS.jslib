var plugin = {

   GetExternalJS: function () {

      var tfjs_script = document.createElement("script");
      tfjs_script.src = "https://cdn.jsdelivr.net/npm/@tensorflow/tfjs@2.0.0/dist/tf.min.js";
      document.head.appendChild(tfjs_script);

      var script = document.createElement("script");
      script.src = "./StreamingAssets/main.js";
      document.head.appendChild(script);
   },

   InitTFJSModel: async function (model_path, backend) {

      // Convert bytes to the text
      var model_path_str = UTF8ToString(model_path);
      var backend_str = UTF8ToString(backend);
      try {
         tf.setBackend(backend_str);
      } catch (error) {
         tf.setBackend('webgl');
      }
      this.model = await tf.loadGraphModel(model_path_str, { fromTFHub: false });

      const input_shape = this.model.inputs[0].shape;
      this.height = input_shape[1];
      this.width = input_shape[2];
      // this.offset = 127.5;
      // this.mean = new Float32Array(buffer, mean, 3);
      // this.std_dev = new Float32Array(buffer, std_dev, 3);
      console.log(`Input Shape: ${input_shape}`);
   },

   PerformInference: function (array_data, size, width, height) {
      if (typeof this.model == 'undefined') {
         console.log(`Session not defined yet (PerformInference)`);
         return;
      }

      // 
      const uintArray = new Uint8ClampedArray(buffer, array_data, size, width, height);
      uintArray.reverse();
      // // 
      // const [redArray, greenArray, blueArray] = new Array(new Array(), new Array(), new Array());
      // // 
      // for (let i = 0; i < uintArray.length; i += 3) {
      //    redArray.push(((uintArray[i + 2] / 255.0) - this.mean[0]) / this.std_dev[0]);
      //    greenArray.push(((uintArray[i + 1] / 255.0) - this.mean[1]) / this.std_dev[1]);
      //    blueArray.push(((uintArray[i] / 255.0) - this.mean[2]) / this.std_dev[2]);
      //    // redArray.push(((uintArray[i] / 255.0) - this.mean[0]) / this.std_dev[0]);
      //    // greenArray.push(((uintArray[i + 1] / 255.0) - this.mean[1]) / this.std_dev[1]);
      //    // blueArray.push(((uintArray[i + 2] / 255.0) - this.mean[2]) / this.std_dev[2]);
      // }
      // // 
      // const input_data = Float32Array.from(redArray.concat(greenArray).concat(blueArray));

      const offset_val = 127.5;
      const [input_array] = new Array(new Array());
      for (let i = 0; i < uintArray.length; i += 3) {
         input_array.push(((uintArray[i]) - offset_val) / offset_val);
         input_array.push(((uintArray[i + 1]) - offset_val) / offset_val);
         input_array.push(((uintArray[i + 2]) - offset_val) / offset_val);
         // skip data[i + 3] to filter out the alpha channel
      }

      const float32Data = Float32Array.from(input_array);

      // 
      PerformInferenceAsync(this.model, float32Data).then(output => {
         this.index = argMax(Array.prototype.slice.call(output));
      })
      return this.index - 1;
   },
}

// Creating functions for the Unity
mergeInto(LibraryManager.library, plugin);