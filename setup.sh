#!/bin/bash

# get webusocket
curl -O https://codeload.github.com/sassembla/WebuSocket/tar.gz/0.8.0
tar -xvf 0.8.0
mv WebuSocket-0.8.0/WebuSocket Assets/majiXA/Client
rm -rf 0.8.0 WebuSocket-0.8.0

# get disquuuun
curl -O https://codeload.github.com/sassembla/Disquuun/tar.gz/0.6.0
tar -xvf 0.6.0
mv Disquuun-0.6.0/Disquuun Assets/majiXA/Server/Editor
rm -rf 0.6.0 Disquuun-0.6.0
