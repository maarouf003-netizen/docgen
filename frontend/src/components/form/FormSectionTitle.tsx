export function FormSectionTitle({ title }: { title: string }) {
  return (
    <h3 className="text-lg font-bold text-amber-700 bg-gray-100 rounded-lg px-4 py-2.5 mb-3 mt-8 first:mt-0">
      {title}
    </h3>
  );
}
