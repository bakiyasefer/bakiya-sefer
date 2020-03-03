source .env && docker run --rm --name=lfs \
 -v=${LFS_CACHE_PATH}:/data \
 -p=8080:8080 \
 -e AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID} \
 -e AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY} \
 -e AWS_DEFAULT_REGION=${AWS_DEFAULT_REGION} \
psxcode/rudolfs --cache-dir=/data --s3-bucket=${LFS_S3_BUCKET} --max-cache-size=${LFS_MAX_CACHE_SIZE}