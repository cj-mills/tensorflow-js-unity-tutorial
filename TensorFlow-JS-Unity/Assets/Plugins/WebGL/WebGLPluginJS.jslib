// Define plugin functions
var plugin = {

   // Add additional JavaScript dependencies to the html page
   GetExternalJS: function () {

      // Add base TensorFlow.js dependencies
      var tfjs_script = document.createElement("script");
      tfjs_script.src = "https://cdn.jsdelivr.net/npm/@tensorflow/tfjs@3.20.0/dist/tf.min.js";
      document.head.appendChild(tfjs_script);

      // Add custom utility functions
      var script = document.createElement("script");
      script.src = "./StreamingAssets/utils.js";
      document.head.appendChild(script);
   },

   // Set the TFJS inference backend
   SetTFJSBackend: function (backend) {
      var backend_str = UTF8ToString(backend);
      try {
         tf.setBackend(backend_str).then(() => { });
      } catch (error) {
         tf.setBackend('webgl');
      }
      console.log(`Inference Backend: ${tf.getBackend()}`)
   },

   // Load a TFJS model
   InitTFJSModel: async function (model_path, mean, std_dev) {

      // Convert bytes to the text
      var model_path_str = UTF8ToString(model_path);

      this.model = await tf.loadGraphModel(model_path_str, { fromTFHub: false });

      const input_shape = this.model.inputs[0].shape;
      console.log(`Input Shape: ${input_shape}`);

      this.mean = new Float32Array(buffer, mean, 3);
      this.std_dev = new Float32Array(buffer, std_dev, 3);
   },

   // Perform inference with the provided image data
   PerformInference: function (image_data, size, width, height) {
      if (typeof this.model == 'undefined') {
         console.log("Model not defined yet");
         return -1;
      }

      // Initialize an array with the raw image data
      const uintArray = new Uint8ClampedArray(buffer, image_data, size, width, height);
      uintArray.reverse();

      // Channels-first order
      // const [redArray, greenArray, blueArray] = new Array(
      //    new Array(),
      //    new Array(),
      //    new Array());
      // for (let i = 0; i < uintArray.length; i += 3) {
      //    redArray.push(((uintArray[i] / 255.0) - this.mean[0]) / this.std_dev[0]);
      //    greenArray.push(((uintArray[i + 1] / 255.0) - this.mean[1]) / this.std_dev[1]);
      //    blueArray.push(((uintArray[i + 2] / 255.0) - this.mean[2]) / this.std_dev[2]);
      // }
      // const float32Data = Float32Array.from(redArray.concat(greenArray).concat(blueArray));
      // const shape = [1, 3, height, width];

      // Channels-last order
      const [input_array] = new Array(new Array());
      for (let i = 0; i < uintArray.length; i += 3) {
         input_array.push(((uintArray[i] / 255.0) - this.mean[0]) / this.std_dev[0]);
         input_array.push(((uintArray[i + 1] / 255.0) - this.mean[1]) / this.std_dev[1]);
         input_array.push(((uintArray[i + 2] / 255.0) - this.mean[2]) / this.std_dev[2]);
      }
      const float32Data = Float32Array.from(input_array);
      const shape = [1, height, width, 3];

      // Pass preprocessed input to the model
      PerformInferenceAsync(this.model, float32Data, shape).then(output => {
         // var results = softmax(Array.prototype.slice.call(output));
         // Extract the predicted class from the model output
         this.index = argMax(Array.prototype.slice.call(output));
      })
      return this.index;
   },
}

// Add plugin functions
mergeInto(LibraryManager.library, plugin);