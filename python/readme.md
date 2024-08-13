## Python wrapper

To have your C# code being able to execute python code, it is better to create a virtual environment, then decide which libraries you need to install.

In Mac/Linux

```bash
python3 -m venv pywrapper
source pywrapper/bin/activate
```

For windows 

```powershell
python -m venv pywrapper
.\pywrapper\Scripts\activate 
```

Then you can install all the libraries you need, usually I install first torch with manual installation something like this

```bash
pip3 install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
```

You can check your nvidia CUDA version (whisper and other libraries can use CUDA) with

```bash
nvcc --version
```

you can handle requirements with easy thanks to pip

```bash
pip freeze > requirements.txt
```

If you modify references or upgrading pip packages or everything you can update the requirements file with

```bash
pip freeze > requirements.txt
```

Then you can create a kernel for jupyter notebooks using the very same environmnent

```bash
pip install ipykernel
python -m ipykernel install --user --name=various
```

Kernel can be removed using 

```bash
jupyter kernelspec remove langchain_experiments
```


