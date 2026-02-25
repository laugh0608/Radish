import { useNavigate, useParams } from 'react-router-dom';
import { Result, AntButton as Button } from '@radish/ui';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { ROUTES } from '@/router';

export const StickerList = () => {
  const navigate = useNavigate();
  const { groupId } = useParams<{ groupId: string }>();

  useDocumentTitle('分组表情管理');

  return (
    <Result
      status="info"
      title={`分组 ${groupId || ''} 的表情管理页开发中`}
      subTitle="下一步将接入批量上传、排序和单个编辑能力。"
      extra={
        <Button
          variant="filled"
          onClick={() => {
            navigate(ROUTES.STICKERS);
          }}
        >
          返回分组列表
        </Button>
      }
    />
  );
};
